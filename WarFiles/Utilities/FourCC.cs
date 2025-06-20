using System.IO;

namespace WarFiles
{
    public struct FourCC
    {
        private char[] m_characters;

        public FourCC(char c0, char c1, char c2, char c3)
        {
            m_characters = new char[4];
            m_characters[0] = c0;
            m_characters[1] = c1;
            m_characters[2] = c2;
            m_characters[3] = c3;
        }

        public void Serialize(BinaryWriter writer)
        {
            if (m_characters != null)
            {
                for (int i = 0; i < m_characters.Length; i++)
                {
                    writer.Write(m_characters[i]);
                }
            }
        }

        public static FourCC Deserialize(BinaryReader reader)
        {
            return new FourCC(reader.ReadChar(), reader.ReadChar(), reader.ReadChar(), reader.ReadChar());
        }

        public override string ToString()
        {
            if (m_characters != null)
            {
                string result = "'";
                for (int i = 0; i < m_characters.Length; i++)
                {
                    result += m_characters[i];
                }
                result += "'";
                return result;
            }

            return "null";
        }
    }
}
