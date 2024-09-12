using System;
using System.IO;
using System.Linq;
using System.Text;

namespace gmdDump
{
    class Program
    {
        static void Main(string[] args)
        {
            //string input = args[0];
            foreach(var arg in args)
            {
                string input = arg;
                if (Path.GetExtension(input) == ".gmd")
                {
                    GmdInput(input);
                }
                else
                {
                    TxtInput(input);
                }
            }
        }

        static void TxtInput(string input)
        {
            string output = Path.GetDirectoryName(input) + "\\" + Path.GetFileNameWithoutExtension(input) + ".table";
            string headerDir = Path.GetDirectoryName(input) + "\\" + Path.GetFileNameWithoutExtension(input) + ".header";
            string gmdOutput = Path.GetDirectoryName(input) + "\\" + Path.GetFileNameWithoutExtension(input) + ".gmd";
            StreamReader reader = File.OpenText(input);

            //headerReader = new BinaryReader(File.OpenRead(headerOutput));
            //createHeader(input, 250, 100);
            //createHeader(string input, int string_count, int table_size)
            if (File.Exists(output))
                File.Delete(output);

            int string_count = File.ReadLines(input).Count();
            using (FileStream fsStream = new FileStream(output, FileMode.Append))
            using (BinaryWriter writer = new BinaryWriter(fsStream, Encoding.UTF8))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Replace("<LINE>", "\r\n");
                    byte[] text = System.Text.Encoding.UTF8.GetBytes(line);
                    Console.WriteLine(line);
                    writer.Write(text);
                    byte padding = 0x0;
                    writer.Write(padding);
                }
            }
            int table_size = (int)new FileInfo(output).Length;
            int header_size = (int)new FileInfo(headerDir).Length;
            Console.WriteLine(string_count);
            Console.WriteLine(table_size);

            createHeader(input, string_count, table_size);

            if (File.Exists(gmdOutput))
                File.Delete(gmdOutput);

            using (FileStream fsStream = new FileStream(gmdOutput, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fsStream, Encoding.UTF8))
            {
                BinaryReader tableReader = new BinaryReader(File.OpenRead(output));
                BinaryReader headerReader = new BinaryReader(File.OpenRead(headerDir));

                byte[] byteBuffer;
                if (table_size >= header_size)
                {
                    byteBuffer = new byte[table_size];
                }
                else
                {
                    byteBuffer= new byte[header_size];
                }

                headerReader.Read(byteBuffer, 0, header_size);
                writer.Write(byteBuffer, 0, header_size);
                tableReader.Read(byteBuffer, 0, table_size);
                writer.Write(byteBuffer, 0, table_size);

                tableReader.Close();
                headerReader.Close();

                /*
                byte[] byteHeader = new byte[(int)table_start];
                //headerReader.Read(byteHeader, 0, (int)table_start);
                headerReader.Read(byteHeader, 0, 0x10); // 0x10 is hardcoded s_count_offset
                writer.Write(byteHeader, 0, 0x10); // Read until s_count
                headerReader.Read(byteHeader, 0, 4);
                writer.Write(string_count); // Write my own s_count
                headerReader.Read(byteHeader, 0, 4);
                writer.Write(byteHeader, 0, 4); // Read 4 more
                headerReader.Read(byteHeader, 0, 4);
                writer.Write(table_size); // Write my own t_size

                int remainingHeaderSize = (int)table_start - (12 + 0x10);
                headerReader.Read(byteHeader, 0, remainingHeaderSize);
                writer.Write(byteHeader, 0, remainingHeaderSize); // Read remaining header
                headerReader.Close();
                */
            }
        }

        public static class fileOffsets
        {
            public static int s_count_offset = 0;
            public static int t_size_offset = 0;
        }

        public static void offsetSetter(String file)
        {
            BinaryReader reader = new BinaryReader(File.OpenRead(file));
            reader.ReadInt32();
            int version = reader.ReadInt32();

            // Handle version offset differences
            if (version == 0x00010201 || version == 0x00010101) // MH3U EU, MH3G JP
            {
                fileOffsets.s_count_offset = 0x10;
                fileOffsets.t_size_offset = 0x18;
            }
            else if (version == 0x00010302 || version == 0x00020301) // MHX JP, MHXX
            {
                fileOffsets.s_count_offset = 0x18;
                fileOffsets.t_size_offset = 0x20;
            }
            else
            {
                Console.WriteLine("ERROR: Unsupported GM version, aborting.");
                return;
            }
            reader.Close();
        }

        static void GmdInput(string input)
        {
            string output = Path.GetDirectoryName(input) + "\\" + Path.GetFileNameWithoutExtension(input) + ".txt";
            string headerOutput = Path.GetDirectoryName(input) + "\\" + Path.GetFileNameWithoutExtension(input) + ".header";
            bool BigEndian = false;
            long input_size = new FileInfo(input).Length;
            BinaryReader reader = new BinaryReader(File.OpenRead(input));
            UInt32 table_start;

            // Handle input / output files
            int header = reader.ReadInt32();
            //int version = reader.ReadInt32();
            offsetSetter(input);

            if (header == 0x00444D47)
            {
                BigEndian = false;
            }
            else if (header == 0x474D4400)
            {
                BigEndian = true;
            }
            else
            {
                Console.WriteLine("ERROR: Invalid input file specified, aborting.");
                return;
            }

            if (File.Exists(output))
                File.Delete(output);
            if (File.Exists(headerOutput))
                File.Delete(headerOutput);

            // Process input file
            UInt32 string_count = 0;
            UInt32 table_size;
            //int s_count_offset = 0;
            //int t_size_offset = 0;

            if (BigEndian == true)
            {
                reader.BaseStream.Seek(0x20, SeekOrigin.Begin);
                string_count = reader.ReadUInt32();
                Console.WriteLine("INFO: string_count " + string_count);
                reader.BaseStream.Seek(0x04, SeekOrigin.Current);
                table_size = reader.ReadUInt32();
                Console.WriteLine("INFO: table_size " + table_size);

                string_count = Helper.swapEndianness(string_count);
                table_size = Helper.swapEndianness(table_size);

                table_start = Convert.ToUInt32(input_size) - table_size;
                reader.BaseStream.Seek(table_start, SeekOrigin.Begin);
            }
            else
            {
                reader.BaseStream.Seek(fileOffsets.s_count_offset, SeekOrigin.Begin);
                string_count = reader.ReadUInt32();
                Console.WriteLine("INFO: string_count " + string_count);
                reader.BaseStream.Seek(fileOffsets.t_size_offset, SeekOrigin.Begin);
                table_size = reader.ReadUInt32();
                Console.WriteLine("INFO: table_size " + table_size);

                table_start = Convert.ToUInt32(input_size) - table_size;
                reader.BaseStream.Seek(table_start, SeekOrigin.Begin);
            }

            // Process strings in string table
            for (int i = 0; i < string_count; i++)
            {
                string str = Helper.readNullterminated(reader).Replace("\r\n", "<LINE>");
                using (StreamWriter writer = new StreamWriter(output, true, Encoding.UTF8))
                {
                    writer.WriteLine(str);
                }
            }

            createHeader(input, (int)string_count, (int)table_size);

            reader.Close();

            Console.WriteLine("INFO: Finished processing " + Path.GetFileName(input) + "!");
        }

        static void createHeader(string input, int string_count, int table_size)
        {
            string headerOutput = Path.GetDirectoryName(input) + "\\" + Path.GetFileNameWithoutExtension(input) + ".header";
            BinaryReader headerReader;
            bool renameAndDelete = false;

            long input_size = new FileInfo(input).Length;
            UInt32 table_start;

            if (File.Exists(headerOutput))
            {
                offsetSetter(headerOutput);
                headerReader = new BinaryReader(File.OpenRead(headerOutput));
                input_size = new FileInfo(headerOutput).Length;
                headerOutput = Path.GetDirectoryName(input) + "\\" + Path.GetFileNameWithoutExtension(input) + "2.header";
                table_start = Convert.ToUInt32(input_size);
                renameAndDelete = true;
            }
            else
            {
                headerReader = new BinaryReader(File.OpenRead(input));
                // Get table_start which is where the header ends
                BinaryReader reader = new BinaryReader(File.OpenRead(input));
                reader.BaseStream.Seek(fileOffsets.t_size_offset, SeekOrigin.Begin);
                UInt32 read_table_size = reader.ReadUInt32();
                Console.WriteLine("INFO: table_size " + read_table_size);
                table_start = Convert.ToUInt32(input_size) - read_table_size;
            }

            using (FileStream fsStream = new FileStream(headerOutput, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fsStream, Encoding.UTF8))
            {
                byte[] byteHeader = new byte[(int)table_start];
                //headerReader.Read(byteHeader, 0, (int)table_start);
                headerReader.Read(byteHeader, 0, fileOffsets.s_count_offset); // 0x10 is hardcoded s_count_offset
                writer.Write(byteHeader, 0, fileOffsets.s_count_offset); // Read until s_count
                headerReader.Read(byteHeader, 0, 4);
                writer.Write(string_count); // Write my own s_count
                headerReader.Read(byteHeader, 0, 4);
                writer.Write(byteHeader, 0, 4); // Read 4 more
                headerReader.Read(byteHeader, 0, 4);
                writer.Write(table_size); // Write my own t_size

                int remainingHeaderSize = (int)table_start - (12 + fileOffsets.s_count_offset);
                headerReader.Read(byteHeader, 0, remainingHeaderSize);
                writer.Write(byteHeader, 0, remainingHeaderSize); // Read remaining header
                headerReader.Close();
            }

            if (renameAndDelete)
            {
                string orgHeader = Path.GetDirectoryName(input) + "\\" + Path.GetFileNameWithoutExtension(input) + ".header";
                File.Delete(orgHeader);
                File.Move(headerOutput, orgHeader);
            }
        }
    }
}
