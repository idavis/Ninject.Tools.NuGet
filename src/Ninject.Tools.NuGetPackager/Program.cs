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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Ionic.Zip;

#endregion

namespace Ninject.Tools.NuGetPackager
{
    public class Program
    {
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
                var packageFileInfo = new FileInfo( args[0] );
                DirectoryInfo rootDirectory = packageFileInfo.Directory;
                string packageFile = args[0].Trim();
                string[] package = args[0].Split( new[] { "_" }, StringSplitOptions.RemoveEmptyEntries );
                string project = package[0].Replace( rootDirectory.FullName, string.Empty ).Trim( new[] { '\\' } );
                string product = package[1];
                string version = package[2];
                string specFile = ( string.Equals( product, "Ninject2" ) ? "Ninject" : product ) + ".nuspec";

                UnZip( rootDirectory, packageFile, product, project, version );
                UpdateSpecFile( rootDirectory, product, version, specFile );
            }
            catch ( Exception ex )
            {
                Console.WriteLine( ex );
            }
        }

        private static void UpdateSpecFile( DirectoryInfo root, string product, string version, string specFile )
        {
            string[] specFileContents =
                    File.ReadAllText( Path.Combine( root.FullName, specFile ) )
                            .Split( new[] { Environment.NewLine }, StringSplitOptions.None );
            for ( int i = 0; i < specFileContents.Length; i++ )
            {
                if ( specFileContents[i].Trim().StartsWith( "<version>" ) )
                {
                    specFileContents[i] = string.Format( "    <version>{0}</version>", version );
                }
            }
            string outputSpecFile = Path.Combine( root.FullName,
                                                  product,
                                                  version,
                                                  specFile );
            File.WriteAllText( outputSpecFile, string.Join( Environment.NewLine, specFileContents ) );
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
    }
}