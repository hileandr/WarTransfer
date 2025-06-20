using System;
using System.Collections.Generic;
using System.IO;

namespace WarFiles.Serialization
{
    public static class SerializeHelper
    {
        // Writers
        public static void WriteString(BinaryWriter writer, string s)
        {
            if (s.Length > 0)
            {
                if (s[s.Length - 1] != '\0')
                {
                    s += '\0';
                }

                for (int i = 0; i < s.Length; i++)
                {
                    writer.Write(s[i]);
                }
            }
        }

        public static void WriteArray(BinaryWriter writer, char[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                writer.Write(arr[i]);
            }
        }

        public static void WriteArray(BinaryWriter writer, float[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                writer.Write(arr[i]);
            }
        }

        public static void WriteArray(BinaryWriter writer, int[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                writer.Write(arr[i]);
            }
        }

        // Readers
        public static string ReadString(BinaryReader reader)
        {
            string s = "";
            char c = '0';

            while (c != '\0')
            {
                c = reader.ReadChar();

                if (c != '\0')
                    s += c;
            }

            return s;
        }

        //public static string ReadString(BinaryReader reader, uint size)
        //{
        //    string s = "";
        //    char c = '0';

        //    for (uint i = 0; i < size; i++)
        //    {
        //        c = reader.ReadChar();
        //    }

        //    while (c != '\0')
        //    {
        //        c = reader.ReadChar();

        //        if (c != '\0')
        //            s += c;
        //    }

        //    return s;
        //}

        public static char[] ReadCharArr(BinaryReader reader, uint size)
        {
            char[] result = new char[size];
            for (uint i = 0; i < size; i++)
            {
                result[i] = (char)reader.ReadByte();
            }

            return result;
        }

        public static int[] ReadIntArr(BinaryReader reader, int size)
        {
            int[] result = new int[size];

            for (int i = 0; i < size; i++)
            {
                result[i] = reader.ReadInt32();
            }

            return result;
        }

        public static float[] ReadFloatArr(BinaryReader reader, int size)
        {
            float[] result = new float[size];

            for (int i = 0; i < size; i++)
            {
                result[i] = reader.ReadSingle();
            }

            return result;
        }
    }
}
