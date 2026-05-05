// =============================================================================
//  CryptoSoft — Chiffrement / déchiffrement XOR
// =============================================================================
//
//  Usage : CryptoSoft.exe "<file_path>"
//
//  Comportement :
//    - Si le fichier existe, il est chiffré (ou déchiffré) par XOR en place.
//    - L'opération XOR est symétrique : appeler deux fois donne le fichier original.
//    - Exit code : 0 = succès,  1 = argument manquant,  2 = fichier introuvable,
//                  3 = erreur d'accès / exception.
//
//  La clé XOR est une constante 32 octets. Pour un usage réel, remplacer par
//  une clé dérivée (PBKDF2, etc.).
//
// =============================================================================

using System;
using System.IO;

namespace CryptoSoft
{
    internal static class Program
    {
        // Clé XOR 32 octets (256 bits).  Modifiez-la pour personnaliser.
        private static readonly byte[] Key =
        {
            0x4C, 0x61, 0x20, 0x73, 0xE9, 0x63, 0x75, 0x72,
            0x69, 0x74, 0xE9, 0x20, 0x64, 0x27, 0x61, 0x62,
            0x6F, 0x72, 0x64, 0x21, 0xAB, 0xCD, 0xEF, 0x01,
            0x23, 0x45, 0x67, 0x89, 0xFE, 0xDC, 0xBA, 0x98
        };

        private static int Main(string[] args)
        {
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.Error.WriteLine("[CryptoSoft] Error: no file path provided.");
                Console.Error.WriteLine("Usage: CryptoSoft.exe \"<file_path>\"");
                return 1;
            }

            string filePath = args[0].Trim('"');

            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"[CryptoSoft] Error: file not found: {filePath}");
                return 2;
            }

            try
            {
                XorFile(filePath);
                Console.WriteLine($"[CryptoSoft] Done: {filePath}");
                return 0;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Error.WriteLine($"[CryptoSoft] Access denied: {ex.Message}");
                return 3;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"[CryptoSoft] I/O error: {ex.Message}");
                return 3;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CryptoSoft] Unexpected error: {ex.Message}");
                return 3;
            }
        }

        /// <summary>
        /// Chiffre ou déchiffre un fichier en place par opération XOR.
        /// L'opération est symétrique : XOR(XOR(data, key), key) == data.
        /// </summary>
        private static void XorFile(string filePath)
        {
            // Lire en mémoire (convient pour les fichiers ≤ quelques Go)
            byte[] data = File.ReadAllBytes(filePath);

            for (int i = 0; i < data.Length; i++)
                data[i] ^= Key[i % Key.Length];

            // Écrire le résultat (remplacement en place)
            File.WriteAllBytes(filePath, data);
        }
    }
}
