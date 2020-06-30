using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParadigmFileExtractor
{
    public static class AsyncWriteHelper
    {
        private static List<Task> writeTasks = new List<Task>();

        public static void WriteAllBytes(string path, byte[] bytes)
        {
            writeTasks.Add(WriteAllBytesAsync(path, bytes));
        }

        public static void WaitForFilesToFinishWriting()
        {
            Task.WhenAll(writeTasks).Wait();
        }

        private static async Task WriteAllBytesAsync(string path, byte[] bytes)
        {
            using (FileStream fStream = new FileStream(path, FileMode.Create))
            {
                await fStream.WriteAsync(bytes, 0, bytes.Length);
            }
        }
    }
}
