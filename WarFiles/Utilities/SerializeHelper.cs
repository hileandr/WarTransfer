using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WarFiles.Serialization
{
    public static class SerializeHelper
    {
        // Writers
        public static void WriteString(BinaryWriter writer, string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                writer.Write('\0');
                return;
            }
            if (s[s.Length - 1] != '\0')
                s += '\0';

            // Slightly faster than per-char Write
            writer.Write(s.ToCharArray());
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

        public static int ReadCount(BinaryReader reader, string fieldName, int maxInclusive)
        {
            int value = reader.ReadInt32();
            if (value < 0 || value > maxInclusive)
                throw new FormatException($"{fieldName}={value} is out of bounds (0..{maxInclusive}). Offset={reader.BaseStream.Position}");
            return value;
        }

        // Readers
        public static string ReadString(BinaryReader reader, StringBuilder sb, int maxChars = 1_000_000)
        {
            sb.Clear();

            int count = 0;
            while (true)
            {
                if (count++ > maxChars)
                    throw new FormatException($"String exceeded max length ({maxChars}). Offset={reader.BaseStream.Position}");

                char c = reader.ReadChar(); // relies on same encoding used by writer
                if (c == '\0') break;
                sb.Append(c);
            }
            return sb.ToString();
        }

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
