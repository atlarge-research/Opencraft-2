using System.Globalization;
using System.IO;

namespace UnityEditor.Performance.ProfileAnalyzer
{
    public static class ProfileAnalyzerExtensions
    {
        public static void ConvertFromProfilerToCsv(this ProfileAnalyzerWindow window, string outputFile, ref int fileOffset, ref double timeOffset)
        {
            // Get profiler frame count
            window.SyncWithProfilerWindow();
            
            window.PullFromProfiler(window.m_ProfilerFirstFrameIndex, window.m_ProfilerLastFrameIndex, window.m_ProfileSingleView, window.m_FrameTimeGraph);
            
            bool isFirst = !File.Exists(outputFile);
            
            int maxFrames = window.m_ProfileSingleView.data.GetFrameCount();
            // Write pulled data to CSV
            using (StreamWriter file = new StreamWriter(outputFile, append:true))
            {
                if(isFirst)
                    file.WriteLine("Frame Index; Frame Time (ms); Time from first frame (ms)");
                
                var frame = window.m_ProfileSingleView.data.GetFrame(0);
                // msStartTime isn't very accurate so we don't use it

                
                for (int frameOffset = 0; frameOffset < maxFrames; frameOffset++)
                {
                    frame = window.m_ProfileSingleView.data.GetFrame(frameOffset);
                    int frameIndex = window.m_ProfileSingleView.data.OffsetToDisplayFrame(frameOffset);
                    frameIndex = window.GetRemappedUIFrameIndex(frameIndex, window.m_ProfileSingleView);

                    float msFrame = frame.msFrame;
                    file.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0};{1};{2}",
                        fileOffset+frameIndex, msFrame, timeOffset));

                    timeOffset += msFrame;
                }
            }

            fileOffset += maxFrames;

        }
        
    }
}

