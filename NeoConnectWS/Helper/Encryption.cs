using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace NeoConnectWS.Helper
{
    public class Encryption
    {
        private static string HashKey = "B9F1";
        public static string HashPassword(string password)
        {
            string encodedPassword = password;
            HMACSHA1 hash = new HMACSHA1();
            hash.Key = HexToByte(HashKey);
            encodedPassword = Convert.ToBase64String(hash.ComputeHash(Encoding.Unicode.GetBytes(password)));

            return encodedPassword;
        }

        private static byte[] HexToByte(string hexString)
        {
            byte[] returnBytes = new byte[hexString.Length / 2];
            for (int i = 0; i < returnBytes.Length; i++)
                returnBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            return returnBytes;
        }
    }
}