using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace WarTransferUI
{
    internal enum SoundType
    {
        Working,
        Error,
        Done
    }

    internal static class SoundManager
    {
        private static SoundPlayer? m_Player;
        private static Stream? m_AudioStream;

        private static readonly IDictionary<SoundType, string> Sounds = new SortedList<SoundType, string>
        {
            { SoundType.Working, "PeonYes3.wav" },
            { SoundType.Error, "Error.wav" },
            { SoundType.Done, "PeonJobDone.wav" }
        };

        internal static void PlaySound(SoundType soundType)
        {
            ClearAudioStream();

            if (Settings.EnableUISounds)
            {
                System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
                string soundName = $"{a.GetName().Name}.Sounds.{Sounds[soundType]}";

                m_AudioStream = a.GetManifestResourceStream(soundName);

                m_Player = new SoundPlayer(m_AudioStream);
                m_Player.Play();
            }
        }

        private static void ClearAudioStream()
        {
            if (m_Player != null)
            {
                m_Player.Stop();
                m_Player.Dispose();
                m_Player = null;
            }

            if (m_AudioStream != null)
            {
                m_AudioStream.Close();
                m_AudioStream.Dispose();
                m_AudioStream = null;
            }
        }
    }
}
