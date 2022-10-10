using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace GE3Packer
{
    public class zlib
    {
        public static byte[] Decompress(byte[] data)
        {
            using MemoryStream compressed = new MemoryStream(data);
            using MemoryStream decompressed = new MemoryStream();
            using InflaterInputStream inputstream = new InflaterInputStream(compressed);
            inputstream.CopyTo(decompressed);
            return decompressed.ToArray();
        }

        public static byte[] Compress(byte[] data)
        {
            using MemoryStream compressed = new MemoryStream();
            using DeflaterOutputStream outputStream = new DeflaterOutputStream(compressed);
            outputStream.Write(data, 0, data.Length);
            outputStream.Close();
            return compressed.ToArray();
        }
    }
}
