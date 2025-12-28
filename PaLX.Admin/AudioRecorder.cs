using System;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;

namespace PaLX.Admin
{
    public class AudioRecorder
    {
        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = CharSet.Ansi)]
        private static extern int mciSendString(string lpstrCommand, StringBuilder lpstrReturnString, int uReturnLength, IntPtr hwndCallback);

        private string _tempFile;

        public void StartRecording()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), "palx_audio_" + DateTime.Now.Ticks + ".wav");
            mciSendString("open new type waveaudio alias recsound", null, 0, IntPtr.Zero);
            mciSendString("set recsound time format ms", null, 0, IntPtr.Zero);
            mciSendString("set recsound bitpersample 16", null, 0, IntPtr.Zero);
            mciSendString("set recsound samplespersec 44100", null, 0, IntPtr.Zero);
            mciSendString("set recsound channels 1", null, 0, IntPtr.Zero);
            mciSendString("set recsound format tag pcm", null, 0, IntPtr.Zero);
            mciSendString("record recsound", null, 0, IntPtr.Zero);
        }

        public string StopRecording()
        {
            mciSendString("stop recsound", null, 0, IntPtr.Zero);
            mciSendString($"save recsound \"{_tempFile}\" wait", null, 0, IntPtr.Zero);
            mciSendString("close recsound", null, 0, IntPtr.Zero);
            return _tempFile;
        }

        public void CancelRecording()
        {
            mciSendString("stop recsound", null, 0, IntPtr.Zero);
            mciSendString("close recsound", null, 0, IntPtr.Zero);
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }
    }
}
