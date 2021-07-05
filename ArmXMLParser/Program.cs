using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace ArmXMLParser
{
    class Program
    {
        static Dictionary<string, EncodingClass> Classes = new Dictionary<string, EncodingClass>();

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                args = new string[] { @"D:\Programming\Arm XML\ISA_A64_xml_v87A-2021-03\encodingindex.xml" };
            }
            else
            {
                Console.WriteLine("Looking for encodingindex.xml");
            }

            Console.WriteLine("Generating Table");

            XmlDocument document = new XmlDocument();

            document.LoadXml(File.ReadAllText(args[0]));

            GetParentClasses(document.DocumentElement);

            GetEncoding(document.DocumentElement);

            StreamWriter writer = new StreamWriter("encodings.txt");

            string[] Keys = UCheck.ToArray();

            foreach (string key in Keys)
            {
                writer.WriteLine(key);
            }

            writer.Close();

            Console.WriteLine("Done.");

            Console.Read();
        }

        struct Operand
        {
            public int top;
            public int len;
        }

        class EncodingClass
        {
            public string Name;

            public char[] Encoding;

            public Dictionary<string, Operand> Operands;

            public override string ToString()
            {
                string Out = "";

                foreach (char c in Encoding)
                {
                    Out += c;
                }

                return $"{Out}";
            }
        }

        static void GetParentClasses(XmlNode parent)
        {
            if (parent.Name == "iclass_sect")
            {
                //Console.WriteLine(parent.Attributes[1].Value);

                List<char> Encodings = new List<char>();

                Dictionary<string, Operand> Operands = new Dictionary<string, Operand>();

                foreach (XmlNode rlook in parent.ChildNodes)
                {
                    if (rlook.Name == "regdiagram")
                    {
                        foreach (XmlNode box in rlook.ChildNodes)
                        {
                            foreach (XmlAttribute attribute in box.Attributes)
                            {
                                if (attribute.Name == "name")
                                {
                                    int size = 1;
                                    int bit = 0;

                                    foreach (XmlAttribute attr in box.Attributes)
                                    {
                                        if (attr.Name == "hibit")
                                        {
                                            bit = int.Parse(attr.Value);
                                        }

                                        if (attr.Name == "width")
                                        {
                                            size = int.Parse(attr.Value);
                                        }
                                    }

                                    Operands.Add(attribute.Value,new Operand() {top = bit, len = size });
                                }
                            }

                            foreach (XmlNode c in box.ChildNodes)
                            {
                                if (c.Attributes.Count == 1)
                                {
                                    int size = int.Parse(c.Attributes[0].Value);

                                    for (int i = 0; i < size; i++)
                                    {
                                        Encodings.Add('-');
                                    }
                                }
                                else
                                {
                                    if (c.InnerText.Contains("1") || c.InnerText.Contains("0"))
                                    {
                                        Encodings.Add(c.InnerText[0]);
                                    }
                                    else
                                    {
                                        Encodings.Add('-');
                                    }
                                }
                            }
                        }
                    }
                }

                Classes.Add(parent.Attributes[1].Value, new EncodingClass() { Name = parent.Attributes[1].Value, Encoding = Encodings.ToArray(), Operands = Operands});
            }

            foreach (XmlNode element in parent.ChildNodes)
            {
                GetParentClasses(element);
            }
        }

        static string CurrentTable;
        static List<string> OperandIndexes;
        static int OperandIndex = 0;

        static string FillString(string source, int wantedSize)
        {
            while (source.Length < wantedSize)
            {
                source += "-";
            }

            return source;
        }

        static HashSet<string> UCheck = new HashSet<string>();

        static void GetEncoding(XmlNode parent)
        {
            if (parent.Name == "iclass_sect")
            {
                CurrentTable = parent.Attributes[1].Value;
            }

            if (parent.Name == "tr")
            {
                if (parent.Attributes[0].Value == "heading2")
                {
                    OperandIndexes = new List<string>();

                    foreach (XmlNode th in parent.ChildNodes)
                    {
                        OperandIndexes.Add(th.InnerText);
                    }
                }

                if (parent.Attributes[0].Value == "instructiontable")
                {
                    EncodingClass CurrentClass = Classes[CurrentTable];

                    OperandIndex = 0;

                    string Name = "";
                    string Tag = "";
                    bool Single = true;

                    Dictionary<string, string> Encodings = new Dictionary<string, string>();

                    List<string> DoesNotEqualCases = new List<string>();

                    foreach (XmlNode td in parent.ChildNodes)
                    {
                        if (td.Attributes[0].Value == "iformname")
                        {
                            Name = td.InnerText;
                        }
                        else if (td.Attributes[0].Value == "enctags")
                        {
                            Tag = td.InnerText;
                        }
                        else if (td.Attributes[0].Name == "bitwidth")
                        {
                            string CurrentEncodingName = OperandIndexes[OperandIndex];

                            int DesiredSize = CurrentClass.Operands[CurrentEncodingName].len;

                            string Encoding = FillString(td.InnerText, DesiredSize).Replace("x", "-");

                            if (Encoding.Contains("!="))
                            {
                                Single = false;

                                DoesNotEqualCases.Add($"{CurrentEncodingName} {Encoding}");

                                Encoding = FillString("", DesiredSize);
                            }

                            Encodings.Add(CurrentEncodingName, Encoding);

                            OperandIndex++;
                        }
                    }

                    char[] EncodingTemp = CurrentClass.Encoding.ToArray();

                    Array.Reverse(EncodingTemp);

                    string[] EncodingKeys = Encodings.Keys.ToArray();

                    foreach (string key in EncodingKeys)
                    {
                        Operand EncodingData = CurrentClass.Operands[key];

                        int top = EncodingData.top;
                        int len = EncodingData.len;

                        int bottom = top - (len - 1);

                        string encoding = Encodings[key];

                        for (int i = 0; i < len; i++)
                        {
                            EncodingTemp[i + bottom] = encoding[(len - 1) - i];
                        }
                    }

                    Array.Reverse(EncodingTemp);

                    StringBuilder str = new StringBuilder();

                    foreach (char c in EncodingTemp)
                    {
                        str.Append(c);
                    }

                    str.Append($";{Name};{CurrentClass.Name};{Tag};");

                    string[] Operands = CurrentClass.Operands.Keys.ToArray();

                    foreach (string operand in Operands)
                    {
                        Operand EncodingData = CurrentClass.Operands[operand];

                        int top = EncodingData.top;
                        int len = EncodingData.len;

                        int bottom = top - (len - 1);

                        str.Append($"{operand} {bottom} {(top - bottom) + 1},");
                    }

                    str.Append("; ");

                    foreach (string Case in DoesNotEqualCases)
                    {
                        str.Append(Case + ", ");
                    }

                    UCheck.Add(str.ToString());
                }
            }

            foreach (XmlNode element in parent.ChildNodes)
            {
                GetEncoding(element);
            }
        }
    }
}
