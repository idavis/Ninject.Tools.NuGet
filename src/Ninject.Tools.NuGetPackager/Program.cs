#region License

// 
// Author: Ian Davis <ian@innovatian.com>
// Copyright (c) 2011, Innovatian Software
// 
// Dual-licensed under the Apache License, Version 2.0, and the Microsoft Public License (Ms-PL).
// See the file LICENSE.txt for details.
// 

#endregion

#region Using Directives

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using Ionic.Zip;
using Ninject.Tools.NuGetPackager.Properties;

#endregion

namespace Ninject.Tools.NuGetPackager
{
    public class Program
    {
        private static readonly string NugetExePath = Path.Combine( AppDomain.CurrentDomain.BaseDirectory, "nuget.exe" );
        private const string DownLoadUrl =
                @"http://teamcity.codebetter.com/guestAuth/repository/downloadAll/{0}/.lastSuccessful/artifacts.zip";
        private static Dictionary<string, string> Urls = new Dictionary<string, string>
                                                         {
                                                                 {
                                                                         "Ninject2",
                                                                         @"bt243"
                                                                         }
                                                         };

        static Program()
        {
            AppDomain.CurrentDomain.AssemblyResolve += Resolver;
        }

        private static Assembly Resolver( object sender, ResolveEventArgs args )
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            Stream resourceStream =
                    executingAssembly.GetManifestResourceStream( "Ninject.Tools.NuGetPackager.Ionic.Zip.dll" );
            Debug.Assert( resourceStream != null );
            var block = new byte[resourceStream.Length];
            resourceStream.Read( block, 0, block.Length );
            Assembly ionicZipAssembly = Assembly.Load( block );
            return ionicZipAssembly;
        }

        public static void Main( string[] args )
        {
            try
            {
                foreach ( var url in Urls )
                {
                    CreatePackage(string.Format(DownLoadUrl, url.Value));
                }

            }
            catch ( Exception ex )
            {
                Console.WriteLine( ex );
            }
        }

        private static void CreatePackage( string url )
        {
            string packageFilePath = DownloadArtifacts( url );
            var packageFileInfo = new FileInfo(packageFilePath);
            DirectoryInfo rootDirectory = packageFileInfo.Directory;
            string packageFile = packageFilePath.Trim();
            string[] package = packageFilePath.Split(new[] { "_" }, StringSplitOptions.RemoveEmptyEntries);
            string project = package[0].Replace(rootDirectory.FullName, string.Empty).Trim(new[] { '\\' });
            string product = package[1];
            string version = package[2];
            string specFile = (string.Equals(product, "Ninject2") ? "Ninject" : product) + ".nuspec";

            UnZip(rootDirectory, packageFile, product, project, version);
            string specFilePath = UpdateSpecFile(rootDirectory, product, version, specFile);
            string specOutputPath = Path.Combine( rootDirectory.FullName, "Specs" );
            CreateNuPkg(specFilePath, specOutputPath);
        }

        private static void CreateNuPkg( string specFilePath, string specOutputPath )
        {
            // nuget pack xunit.nuspec –b c:\xunit  -o c:\xunit-nuget
            ProcessStartInfo processStartInfo = new ProcessStartInfo();
            processStartInfo.CreateNoWindow = true;
            processStartInfo.FileName = NugetExePath;
            processStartInfo.Arguments = string.Format( "pack \"{0}\" -o \"{1}\"",
                                                        specFilePath,
                                                        specOutputPath );
            
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardOutput = true;

            var process = Process.Start( processStartInfo );
            process.WaitForExit();
        }

        private static string DownloadArtifacts( string url )
        {
            var request = (HttpWebRequest) WebRequest.Create( url );
            request.Method = "GET";
            request.UserAgent =
                    "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; InfoPath.2; .NET4.0C; .NET4.0E)";
            var response = (HttpWebResponse) request.GetResponse();
            string fileName = response.Headers
                    .Get( "Content-disposition" )
                    .Replace( "attachment; filename=", string.Empty );
            Console.WriteLine( fileName );
            string filePath = Path.Combine( AppDomain.CurrentDomain.BaseDirectory, fileName );
            using ( Stream responseStream = response.GetResponseStream() )
            {
                if(File.Exists( filePath ))
                {
                    File.Delete( filePath );
                }
                using ( var fileStream = new FileStream( filePath, FileMode.Create ) )
                {
                    CopyStream( responseStream, fileStream );
                }
            }
            return filePath;
        }

        private static void CopyStream( Stream source, Stream destination )
        {
            var buffer = new byte[8 * 1024];
            int len;
            while ( ( len = source.Read( buffer, 0, buffer.Length ) ) > 0 )
            {
                destination.Write( buffer, 0, len );
            }
        }

        public static void UnZip( DirectoryInfo root, string fileName, string product, string project, string version )
        {
            string outputFolder = Path.Combine( root.FullName, product, version, "lib" );
            if ( Directory.Exists( outputFolder ) )
            {
                var directoryInfo = new DirectoryInfo( outputFolder );
                directoryInfo.Attributes = directoryInfo.Attributes & ~FileAttributes.ReadOnly;
                directoryInfo.Delete( true );
            }
            UnzipArtifactPackage( outputFolder, fileName );
            UnzipArtifacts( product, version, outputFolder, project );
        }

        private static void UnzipArtifacts( string product, string version, string outputFolder, string project )
        {
            string[] tempZips = Directory.GetFiles( outputFolder );
            foreach ( string file in tempZips )
            {
                using ( ZipFile zipFile = ZipFile.Read( file ) )
                {
                    var fileInfo = new FileInfo( zipFile.Name );
                    string platform =
                            fileInfo.Name.Replace( product, string.Empty )
                                    .Replace( version, string.Empty )
                                    .Replace( project, string.Empty )
                                    .Replace( "release", string.Empty )
                                    .Replace( ".zip", string.Empty )
                                    .Replace( "-no-web", "CP" )
                                    .Replace( "silverlight-", "SL" )
                                    .Replace( "SL2.0", "SL2" )
                                    .Replace( "3.0", "3" )
                                    .Replace( "4.0", "4" )
                                    .Trim( new[] { '-' } );
                    string platformDir = Path.Combine( outputFolder, platform );
                    foreach ( ZipEntry zipEntry in zipFile )
                    {
                        zipEntry.Extract( platformDir, ExtractExistingFileAction.OverwriteSilently );
                    }
                }
            }
            foreach ( string tempZip in tempZips )
            {
                File.Delete( tempZip );
            }
        }

        private static void UnzipArtifactPackage( string outputFolder, string fileName )
        {
            using ( ZipFile zipFile = ZipFile.Read( fileName ) )
            {
                // here, we extract every entry, but we could extract conditionally
                // based on entry name, size, date, checkbox status, etc.  
                foreach ( ZipEntry zipEntry in zipFile )
                {
                    if ( zipEntry.FileName.EndsWith( "source.zip" ) )
                    {
                        continue;
                    }
                    zipEntry.Extract( outputFolder, ExtractExistingFileAction.OverwriteSilently );
                }
            }
        }

        private static string UpdateSpecFile(DirectoryInfo root, string product, string version, string specFile)
        {
            byte[] data = (byte[])typeof(Resources).GetProperty( specFile.Replace( ".nuspec", string.Empty ) ).GetValue( null, null );
            string contents = Encoding.UTF8.GetString( data );
            
            string[] specFileContents =
                    contents.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            for (int i = 0; i < specFileContents.Length; i++)
            {
                if (specFileContents[i].Trim().StartsWith("<version>"))
                {
                    specFileContents[i] = string.Format("    <version>{0}</version>", version);
                }
            }
            string outputSpecFile = Path.Combine(root.FullName,
                                                  product,
                                                  version,
                                                  specFile);
            File.WriteAllText(outputSpecFile, string.Join(Environment.NewLine, specFileContents));
            return outputSpecFile;
        }
    }
}