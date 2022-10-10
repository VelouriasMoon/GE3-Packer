using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GovanifY;

namespace GE3Packer
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args[0].ToLower() == "-e" || args[0].ToLower() == "--extract")
            {
                Console.WriteLine($"Extracting {Path.GetFileNameWithoutExtension(args[1])}");
                PKFile pk = new PKFile();
                pk.Read(args[1]);
                pk.Extract();
                return;
            }
            else if (args[0].ToLower() == "-p" || args[0].ToLower() == "--pack")
            {
                Console.WriteLine("Packing not yet supported.");
                //PKFile pk = new PKFile();
                //pk.Create(args[1]);
                return;
            }

            FileAttributes fileAttributes = File.GetAttributes(args[0]);
            if (fileAttributes.HasFlag(FileAttributes.Directory))
            {
                Console.WriteLine("Packing not yet supported.");
                //PKFile pk = new PKFile();
                //pk.Create(args[0]);
                return;
            }
            else
            {
                Console.WriteLine($"Extracting {Path.GetFileNameWithoutExtension(args[0])}");
                PKFile pk = new PKFile();
                pk.Read(args[0]);
                pk.Extract();
                return;
            }

        }
    }

    public class PKFile
    {
        private string BaseName { get; set; }
        public PKHeader Header { get; set; } = new PKHeader();
        public PKFileSystem FileSystem { get; set; } = new PKFileSystem();
        public byte[] Data { get; set; }

        public void Read(string infile)
        {
            //Check to make sure that all the files are present in the same folder
            BaseName = Path.ChangeExtension(infile, null);
            if (!File.Exists(Path.ChangeExtension(infile, "pfs")) && !File.Exists(Path.ChangeExtension(infile, "pkh")))
            {
                Console.WriteLine(".pk, .pkh, and or .pfs file is missing");
                return;
            }

            //load the raw file data
            Data = File.ReadAllBytes($"{BaseName}.pk");

            //Read Header info
            using (FileStream ms = new FileStream($"{BaseName}.pkh", FileMode.Open))
            {
                using (BinaryStream bs = new BinaryStream(ms, false))
                {
                    Console.WriteLine("Reading Header File...");
                    Header.Read(bs);
                    Console.WriteLine($"{Header.FileCount} Files Detected...");
                }
            }

            //Read FileSystem
            using (FileStream ms = new FileStream($"{BaseName}.pfs", FileMode.Open))
            {
                using (BinaryStream bs = new BinaryStream(ms, false))
                {
                    Console.WriteLine("Reading File System File...");
                    FileSystem.Read(bs);
                    Console.WriteLine($"{FileSystem.NumOfDirectories} Directories Detected...");
                }
            }
        }

        public void Extract()
        {
            if (Directory.Exists(BaseName))
                Directory.Delete(BaseName, true);
            Directory.CreateDirectory(BaseName);
            //Unpack Files
            foreach (PKFileSystem.DirectoryInfos directory in FileSystem.directories)
            {
                if (FileSystem.DirectoryStringTable[directory.Num] == "")
                    continue;

                string path = FileSystem.DirectoryStringTable[directory.Num];
                int parent = directory.Parent;
                while (parent != -1)
                {
                    path = Path.Combine(FileSystem.DirectoryStringTable[parent], path);
                    parent = FileSystem.directories[parent].Parent;
                }
                Directory.CreateDirectory(Path.Combine(BaseName, path));

                if (directory.ChildFileCount > 0)
                {
                    for (int i = 0; i < directory.ChildFileCount; i++)
                    {
                        //File names are stored in table order of the directory that uses them
                        string name = FileSystem.FileStringTable[directory.StartChildFile + i];

                        //Get the hash to find the correct header entry
                        uint hash = CRC32.Hash(Path.Combine(path, name));
                        PKHeader.FileInfos fileInfos = Header.files.Where(x => x.Hash == hash).First();
                        if (fileInfos == null)
                        {
                            Console.WriteLine($"Failed to find info for {Path.Combine(path, name)}, hash calulated {hash}");
                            continue;
                        }

                        //use header info to find and decompress the data from the main pk file
                        //if compressed size is 0 then the data is stored decompressed
                        if (fileInfos.CompressedSize == 0)
                        {
                            byte[] data = new byte[fileInfos.DecompressedSize];
                            Array.Copy(Data, fileInfos.FileOffset, data, 0, fileInfos.DecompressedSize);
                            File.WriteAllBytes(Path.Combine(Path.Combine(BaseName, path), name), data);
                        }
                        else
                        {
                            byte[] data = new byte[fileInfos.CompressedSize];
                            Array.Copy(Data, fileInfos.FileOffset, data, 0, fileInfos.CompressedSize);
                            byte[] decmp = zlib.Decompress(data);
                            if (fileInfos.DecompressedSize != decmp.Length)
                            {
                                Console.WriteLine($"Decompressed size mismatch for {Path.Combine(path, name)}, expected {fileInfos.DecompressedSize}, got {decmp.Length}");
                                continue;
                            }
                            Console.WriteLine($"0x{hash:X8} Extracted to {Path.Combine(Path.Combine(BaseName, path), name)}...");
                            File.WriteAllBytes(Path.Combine(Path.Combine(BaseName, path), name), decmp);
                        }
                    }
                }
            }
            Console.WriteLine("Done.");
        }

        public void Create(string inpath)
        {
            //Calculate directories and files
            BaseName = inpath;
            string[] directories = Directory.GetDirectories(inpath, "*", SearchOption.AllDirectories);
            for (int i = 0; i < directories.Length; i++)
            {
                directories[i] = directories[i].Replace($"{BaseName}", "mount");
            }
            string[] files = Directory.GetFiles(inpath, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                files[i] = files[i].Replace($"{BaseName}", "mount");
            }

            int lookup = 0;
            Dictionary<int, string> DirectoryLookup = new Dictionary<int, string>();
            DirectoryLookup.Add(lookup++, "mount");

            PKFileSystem PFS = new PKFileSystem();
            PFS.NumOfDirectories = directories.Length + 1; //Add one to the list for the empty mount point
            PFS.NumOfFiles = files.Length;
            PFS.directories.Add(new PKFileSystem.DirectoryInfos() { Num = 0, Parent = -1 });
            foreach (string dir in directories)
            {
                //string[] files 
                string[] paths = dir.Split(Path.DirectorySeparatorChar);
            }

            Console.WriteLine();
        }
    }

    public class PKFileSystem
    {
        public int NumOfDirectories { get; set; }
        public int NumOfFiles { get; set; }
        public List<DirectoryInfos> directories { get; set; } = new List<DirectoryInfos>();
        public List<string> DirectoryStringTable { get; set; } = new List<string>();
        public List<string> FileStringTable { get; set; } = new List<string>();


        public void Read(BinaryStream stream)
        {
            long padding = stream.ReadInt64();
            NumOfDirectories = stream.ReadInt32();
            NumOfFiles = stream.ReadInt32();

            //Read Directory Info
            for (int i = 0; i < NumOfDirectories; i++)
            {
                DirectoryInfos directory = new DirectoryInfos();
                directory.Read(stream);
                directories.Add(directory);
            }

            //Read string table index list
            List<int> DirstringIndices = new List<int>();
            List<int> FilestringIndices = new List<int>();
            for (int i = 0; i < NumOfDirectories; i++)
            {
                int index = stream.ReadInt32();
                DirstringIndices.Add(index);
            }
            //Read string table index list
            for (int i = 0; i < NumOfFiles; i++)
            {
                int index = stream.ReadInt32();
                FilestringIndices.Add(index);
            }

            //Read String Table
            foreach (int index in DirstringIndices)
            {
                string s = stream.ReadString();
                DirectoryStringTable.Add(s);
            }
            //Read String Table
            foreach (int index in FilestringIndices)
            {
                string s = stream.ReadString();
                FileStringTable.Add(s);
            }

        }

        public void Write(BinaryStream stream)
        {
            stream.Write((long)0);
            stream.Write(NumOfDirectories);
            stream.Write(NumOfFiles);
            foreach (DirectoryInfos directory in directories)
            {
                directory.Write(stream);
            }
            //write dummy pointer section;
            long pointersection = stream.Tell();
            for (int i = 0; i < NumOfDirectories + NumOfFiles; i++)
            {
                stream.Write((int)0);
            }
            long pos = stream.Tell();
            List<int> stringPostions = new List<int>();
            foreach (string s in DirectoryStringTable)
            {
                stringPostions.Add((int)(stream.Tell() - pos));
                stream.Write(s);
                stream.Write((byte)0);
            }
            foreach (string s in FileStringTable)
            {
                stringPostions.Add((int)(stream.Tell() - pos));
                stream.Write(s);
                stream.Write((byte)0);
            }
            stream.Seek(pointersection, SeekOrigin.Begin);
            foreach (int position in stringPostions)
            {
                stream.Write(position);
            }
        }

        public class DirectoryInfos
        {
            public int Num { get; set; }
            public int Parent { get; set; }
            public int StartChildDirectory { get; set; }
            public int ChildDirectoryCount { get; set; }
            public int StartChildFile { get; set; }
            public int ChildFileCount { get; set; }

            public void Read(BinaryStream stream)
            {
                Num = stream.ReadInt32();
                Parent = stream.ReadInt32();
                StartChildDirectory = stream.ReadInt32();
                ChildDirectoryCount = stream.ReadInt32();
                StartChildFile = stream.ReadInt32();
                ChildFileCount = stream.ReadInt32();
            }

            public void Write(BinaryStream stream)
            {
                stream.Write((int)Num);
                stream.Write((int)Parent);
                stream.Write((int)StartChildDirectory);
                stream.Write((int)ChildDirectoryCount);
                stream.Write((int)StartChildFile);
                stream.Write((int)ChildFileCount);
            }
        }
    }

    public class PKHeader
    {
        public string Magic { get; set; } = "";
        public int FileCount { get; set; }
        public List<FileInfos> files { get; set; } = new List<FileInfos>();

        public void Read(BinaryStream stream)
        {
            Magic = stream.ReadString();
            FileCount = stream.ReadInt32();

            for (int i = 0; i < FileCount; i++)
            {
                FileInfos info = new FileInfos();
                info.Read(stream);
                files.Add(info);
            }
        }

        public void Write(BinaryStream stream)
        {
            stream.TextEncoding = Encoding.UTF8;
            stream.Write("PKH");
            stream.Write((byte)0);
            stream.Write(FileCount);
            foreach (var file in files)
            {
                file.Write(stream);
            }
        }

        public class FileInfos
        {
            public uint Hash { get; set; }
            public int Unk { get; set; }
            public long FileOffset { get; set; }
            public int DecompressedSize { get; set; }
            public int CompressedSize { get; set; }

            public void Read(BinaryStream stream)
            {
                Hash = stream.ReadUInt32();
                Unk = stream.ReadInt32();
                FileOffset = stream.ReadInt64();
                DecompressedSize = stream.ReadInt32();
                CompressedSize = stream.ReadInt32();
            }

            public void Write(BinaryStream stream)
            {
                stream.Write(Hash);
                stream.Write(Unk);
                stream.Write(FileOffset);
                stream.Write(DecompressedSize);
                stream.Write(CompressedSize);
            }
        }
    }
}