using System;
using System.Collections.Generic;
using System.Text;
using CrossInstaller.System;
using System.Net;

namespace CrossInstaller
{
    public class FTPServicer
    {
        /// <summary>
        /// static string field containing the default
        /// </summary>
        public static string DefaultFolderPath = "UltimateModManager/mods/";

        private string URLRoot { get; set; }
        
        public FTPServicer(string ipv4, string port)
        {
            URLRoot = $"ftp://{ipv4}:{port}/{DefaultFolderPath}";
        }
        public FTPServicer(string ipv4, string port, string folder)
        {
            URLRoot = $"ftp://{ipv4}:{port}/{folder}";
        }
    }
}
