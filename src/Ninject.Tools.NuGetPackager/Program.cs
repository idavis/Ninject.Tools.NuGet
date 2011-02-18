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
using System.Linq;
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
        private const string DownLoadUrl =
                @"http://teamcity.codebetter.com/guestAuth/repository/downloadAll/{0}/.lastSuccessful/artifacts.zip";

        private static string NinjectVersion;

        private static readonly string SpecOutputPath = Path.Combine( AppDomain.CurrentDomain.BaseDirectory, "Specs" );
        private static readonly string NugetExePath = Path.Combine( AppDomain.CurrentDomain.BaseDirectory, "NuGet.exe" );

        private static readonly Dictionary<string, string> Urls = new Dictionary<string, string>
                                                                  {
                                                                          { "Ninject2", @"bt243" },
                                                                          {
                                                                                  "Ninject.Extensions.bbvEventBroker",
                                                                                  @"bt248"
                                                                                  },
                                                                          { "Ninject.Extensions.ChildKernel", @"bt244" },
                                                                          {
                                                                                  "Ninject.Extensions.Contextpreservation"
                                                                                  ,
                                                                                  @"bt245"
                                                                                  },
                                                                          { "Ninject.Extensions.Conventions", @"bt249" },
                                                                          {
                                                                                  "Ninject.Extensions.DependencyCreation"
                                                                                  ,
                                                                                  @"bt247"
                                                                                  },
                                                                          {
                                                                                  "Ninject.Extensions.Interception",
                                                                                  @"bt286"
                                                                                  },
                                                                          { "Ninject.Extensions.Logging", @"bt266" },
                                                                          {
                                                                                  "Ninject.Extensions.MessageBroker",
                                                                                  @"bt279"
                                                                                  },
                                                                          { "Ninject.Extensions.NamedScope", @"bt246" },
                                                                          { "Ninject.Extensions.Wcf", @"bt260" },
                                                                          {
                                                                                  "Ninject.Extensions.WeakEventMessageBroker"
                                                                                  ,
                                                                                  @"bt280"
                                                                                  },
                                                                          { "Ninject.Extensions.Wf", @"bt296" },
                                                                          { "Ninject.Extensions.Xml", @"bt268" },
                                                                          { "Ninject.MockingKernel", @"bt272" },
                                                                          { "Ninject.Web", @"bt267" },
                                                                          { "Ninject.Web.Mvc_1", @"bt252" },
                                                                          { "Ninject.Web.Mvc_2", @"bt251" },
                                                                          { "Ninject.Web.Mvc_3", @"bt293" },
                                                                          {
                                                                                  "Ninject.Web.Mvc.FluentValidation",
                                                                                  @"bt270"
                                                                                  },
                                                                          { "Ninject1", @"bt4" },
                                                                  };

        private static bool _Download = true;
        private static bool _Generate = true;
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
            if(args.Length == 1)
            {
                if(string.Equals(args[0], "download"))
                {
                    _Generate = false;
                }
                else if(string.Equals( args[0], "generate" ))
                {
                    _Download = false;
                }
            }
            DeleteDirectory( SpecOutputPath );
            CreateDirectory( SpecOutputPath );

            string[] urlSegments = GetUrlSegments();

            foreach ( string segment in urlSegments )
            {
                try
                {
                    CreatePackage( segment );
                }
                catch ( Exception ex )
                {
                    Console.WriteLine( ex );
                }
            }
        }

        private static string[] GetUrlSegments()
        {
            return Urls.Values.Cast<string>().ToArray();
        }

        private static void CreatePackage( string urlSegment )
        {
            string packageFilePath = null;
            if(_Download)
            {
                packageFilePath = DownloadArtifacts( string.Format( DownLoadUrl, urlSegment ) );
            }
            else
            {
                var currentProject = Urls.Where( item => item.Value == urlSegment ).First();
                var zipFiles = Directory.GetFiles( AppDomain.CurrentDomain.BaseDirectory, "*.zip" ).ToList();
                packageFilePath =zipFiles
                                .Where( file =>
                                        file.ToUpperInvariant().Contains( currentProject.Key.ToUpperInvariant() + "_" ) )
                                .FirstOrDefault();
                if(string.IsNullOrEmpty( packageFilePath ))
                {
                    throw new Exception(string.Format("Zip file not found for {0}:{1}.", currentProject.Key,
                                                      currentProject.Value));
                }
            }
            if(!_Generate)
            {
                return;
            }
            if(packageFilePath.Contains( "MVC" ))
            {
                Console.WriteLine("");
            }
            var packageFileInfo = new FileInfo( packageFilePath );
            DirectoryInfo rootDirectory = packageFileInfo.Directory;
            string packageFile = packageFilePath.Trim();
            string[] package = packageFilePath.Split( new[] { "_" }, StringSplitOptions.RemoveEmptyEntries );
            string project = package[0].Replace( rootDirectory.FullName, string.Empty ).Trim( new[] { '\\' } );
            string product = package[1];
            string version;
            if (string.Equals(product, "Ninject.Web.Mvc"))
            {
                product += package[2];
                version = package[3];
            }
            else
            {
                version = package[2];
            }

            if ( string.Equals( product, "Ninject2" ) )
            {
                NinjectVersion = version;
            }

            string specFile = ( string.Equals( product, "Ninject2" ) ? "Ninject" : product ) + ".nuspec";

            UnZip( rootDirectory, packageFile, product, project, version );
            string specFilePath = UpdateSpecFile( rootDirectory, product, version, specFile );

            CreateNuPkg( specFilePath );
        }

        private static void CreateNuPkg( string specFilePath )
        {
            // nuget pack xunit.nuspec –b c:\xunit  -o c:\xunit-nuget
            var processStartInfo = new ProcessStartInfo();
            processStartInfo.CreateNoWindow = true;
            processStartInfo.FileName = NugetExePath;
            processStartInfo.Arguments = string.Format( "pack \"{0}\" -o \"{1}\"",
                                                        specFilePath,
                                                        SpecOutputPath );

            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;

            Process process = Process.Start( processStartInfo );
            process.WaitForExit();
            string error = process.StandardError.ReadToEnd();
            string info = process.StandardOutput.ReadToEnd();
            Console.WriteLine( string.IsNullOrEmpty( error ) ? info : error );
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

            if ( File.Exists( filePath ) )
            {
                File.Delete( filePath );
            }

            using ( Stream responseStream = response.GetResponseStream() )
            {
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
            DeleteDirectory( outputFolder );
            UnzipArtifactPackage( outputFolder, fileName );
            UnzipArtifacts( product, version, outputFolder, project );
        }

        public static bool DeleteDirectory(string targetDir)
        {
            if (!Directory.Exists(targetDir))
            {
                return false;
            }

            string[] files = Directory.GetFiles(targetDir);
            string[] dirs = Directory.GetDirectories(targetDir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(targetDir, false);
            return true;
        }

        private static void CreateDirectory( string outputFolder )
        {
            if ( Directory.Exists( outputFolder ) )
            {
                return;
            }
            Directory.CreateDirectory( outputFolder );
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
                        fileInfo.Name.Replace(product, string.Empty)
                            .Replace("Ninject.Extensions.ContextPreservation", string.Empty)
                            .Replace("Ninject.Web.Mvc", string.Empty)
                            .Replace(project, string.Empty)
                            .Replace(version, string.Empty)
                            .Replace("release", string.Empty)
                            .Replace(".zip", string.Empty)
                            .Replace("-no-web", "-client")
                            .Replace("silverlight-", "SL")
                            .Replace("SL2.0", "SL2")
                            .Replace("3.0", "3")
                            .Replace("4.0", "4")
                            .Replace("2.0", "2")
                            .Replace("net-4", ".NetFramework 4.0")
                            .Replace("net-3.5", ".NetFramework 3.5")
                            .Replace("net-2.0", ".NetFramework 2.0")
                            .Trim(new[] {'-'});
                    
                    if(platform.Contains("mono") || platform.Contains("-client") || platform.Contains("cf"))
                    {
                        continue;
                    }
                    string platformDir = Path.Combine( outputFolder, platform );
                    foreach ( ZipEntry zipEntry in zipFile )
                    {
                        zipEntry.Extract( platformDir, ExtractExistingFileAction.OverwriteSilently );
                        var libPath = Path.Combine(platformDir, "lib");
                        DeleteDirectory(libPath);
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

        private static string UpdateSpecFile( DirectoryInfo root, string product, string version, string specFile )
        {
            string id = specFile.Replace( ".nuspec", string.Empty ).Replace( ".", "_" );
            PropertyInfo property = typeof (Resources).GetProperties().Where( propertyInfo=>propertyInfo.Name.ToUpperInvariant() == id.ToUpperInvariant() ).FirstOrDefault();
            if ( property == null )
            {
                Console.WriteLine( "Property was null..." );
            }
            var data = (byte[]) property.GetValue( null, null );
            string contents = Encoding.UTF8.GetString( data );

            string[] specFileContents =
                    contents.Split( new[] { Environment.NewLine }, StringSplitOptions.None );
            for ( int i = 0; i < specFileContents.Length; i++ )
            {
                if ( specFileContents[i].Trim().StartsWith( "<version>" ) )
                {
                    specFileContents[i] = string.Format( "    <version>{0}</version>", version );
                }
                else if ( specFileContents[i].Trim().Contains( "{NinjectVersion}" ) )
                {
                    specFileContents[i] = specFileContents[i].Replace( "{NinjectVersion}", NinjectVersion );
                }
            }
            string outputSpecFile = Path.Combine( root.FullName,
                                                  product,
                                                  version,
                                                  specFile );
            File.WriteAllText( outputSpecFile, string.Join( Environment.NewLine, specFileContents ) );
            return outputSpecFile;
        }
    }
}