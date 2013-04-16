using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace Kiosk
{
    class Crypto
    {

        public enum HashType
        {
            SHA1, //more secure
            MD5 //faster
        }

        private static string m_hType;
        private static int m_keyLength;
        private static int m_iterations;
        private static string m_initVector;
        private static string m_passPhrase;
        private static string m_saltValue;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hType">Hashing Algorithm used (SHA1 or MD5)</param>
        /// <param name="keyLength">Size of encryption key in bits. Allowed values are: 128, 192, and 256.</param>
        /// <param name="iterations">Number of iterations used to generate password.</param>
        /// <param name="initVector">16 ASCII character Initialization Vector</param>
        /// <param name="passPhrase">Unicode phrase used to generate encryption string</param>
        /// <param name="saltValue">Unicode value to salt data with (secondary key)</param>
        public Crypto(HashType hType, int keyLength, int iterations, string initVector, string passPhrase, string saltValue)
        {
            m_hType = hType.ToString();

            m_keyLength = keyLength;
            m_iterations = iterations;
            m_initVector = initVector;

            m_passPhrase = passPhrase;
            m_saltValue = saltValue;
        }

        /// <summary>
        /// Encrypts plaintext using Rijndael symetric key algorithm
        /// </summary>
        /// <param name="plainText">Unicode String to be encrypted</param>
        /// <returns>Encrypted value as base64 string</returns>
        public string encryptAES(string plainText)
        {
            byte[] initVectorBytes = Encoding.ASCII.GetBytes(m_initVector);
            byte[] saltValueBytes = Encoding.Unicode.GetBytes(m_saltValue);

            byte[] plainTextBytes = Encoding.Unicode.GetBytes(plainText);

            //generate hash
            PasswordDeriveBytes password = new PasswordDeriveBytes( m_passPhrase, saltValueBytes, m_hType, m_iterations);

            //gets semi-random bytes
            byte[] keyBytes = password.GetBytes(m_keyLength / 8);

            RijndaelManaged symmetricKey = new RijndaelManaged();
            symmetricKey.Mode = CipherMode.CBC;
            ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes);

            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);

            //encrypt
            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            cryptoStream.FlushFinalBlock();

            byte[] cipherTextBytes = memoryStream.ToArray();

            memoryStream.Close();
            cryptoStream.Close();

            string cipherText = Convert.ToBase64String(cipherTextBytes);

            return cipherText;
        }

        /// <summary>
        /// Decrypts encrypted text using Rijndael symetric key algorithm
        /// </summary>
        /// <param name="encryptedText">base64-encoded encrypted text</param>
        /// <returns>Decrypted string as Unicode plain text</returns>
        public string decryptAES(string encryptedText)
        {

            byte[] initVectorBytes = Encoding.ASCII.GetBytes(m_initVector);
            byte[] saltValueBytes = Encoding.Unicode.GetBytes(m_saltValue);

            byte[] cipherTextBytes = Convert.FromBase64String(encryptedText);

            // Create password
            PasswordDeriveBytes password = new PasswordDeriveBytes(m_passPhrase, saltValueBytes, m_hType, m_iterations);

            byte[] keyBytes = password.GetBytes(m_keyLength / 8);

            RijndaelManaged symmetricKey = new RijndaelManaged();

            //use cipher block chaining with defaults
            symmetricKey.Mode = CipherMode.CBC;

            // Generate decryptor from the existing key bytes and initialization 
            // vector. Key size will be defined based on the number of the key 
            // bytes.
            ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes);

            MemoryStream memoryStream = new MemoryStream(cipherTextBytes);

            CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);

            // Since at this point we don't know what the size of decrypted data
            // will be, allocate the buffer long enough to hold ciphertext;
            // plaintext is never longer than ciphertext.
            byte[] plainTextBytes = new byte[cipherTextBytes.Length];

            // Start decrypting.
            int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);

            memoryStream.Close();
            cryptoStream.Close();

            // Convert decrypted data into a string (assuming original was ASCII) 
            string plainText = Encoding.Unicode.GetString(plainTextBytes, 0, decryptedByteCount); 
            return plainText;
        }
    }
}
