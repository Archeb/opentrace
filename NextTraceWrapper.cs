﻿using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace traceroute
{
    internal class NextTraceWrapper
    {
        private Process _process;
        public event EventHandler OnAppQuit;
        public ObservableCollection<TracerouteResult> Output { get; } = new ObservableCollection<TracerouteResult>();
        private int queries = 3;

        public NextTraceWrapper(string arguments)
        {
            Task.Run(() =>
            {
                
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "nexttrace.exe",
                        Arguments = arguments,
                        UseShellExecute = false,
                        StandardOutputEncoding = Encoding.GetEncoding(65001),
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                int readRestLine = 0;

                Regex match1stLine = new Regex(@"^\d{1,2}\|");
                string tracerouteBlock = "";
                _process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        // 去除输出中的控制字符
                        Regex formatCleanup = new Regex(@"(\x9B|\x1B\[)[0-?]*[ -\/]*[@-~]");
                        string line = formatCleanup.Replace(e.Data, "");

                        // 忽略一些行
                        if (line.StartsWith("NextTrace v")) return;
                        if (line.StartsWith("IP Geo Data Provider")) return;
                        if (line.StartsWith("[NextTrace API]")) return;
                        if (line.StartsWith("traceroute to")) return;

                        if (readRestLine > 0)
                        {
                            readRestLine--;

                            if (readRestLine == 0)
                            {
                                tracerouteBlock += line;
                                Output.Add(ProcessBlock(tracerouteBlock));
                            }
                            else
                            {
                                tracerouteBlock += line + "\n";
                            }
                            return;
                        }
                        Match match1 = match1stLine.Match(line);
                        if (match1.Success)
                        {
                            // 以块为单位处理
                            tracerouteBlock = line + "\n";
                            readRestLine = queries - 1;
                        }
                    }
                };
                _process.Start();
                _process.BeginOutputReadLine();
                _process.WaitForExit();
                OnAppQuit?.Invoke(this, null);
            });
        }
        private TracerouteResult ProcessBlock(string block)
        {
            string No = "";
            string IP = "*";
            string Time = "";
            string Geolocation = "";
            string AS = "";
            string Hostname = "";
            string Organization = "";
            string Latitude = "";
            string Longitude = "";
            Debug.Print("NewBlock");
            String[] blockLines = block.Split("\n");
            foreach (string line in blockLines)
            {
                Debug.Print(line);
                String[] LineData = line.Split("|");
                if (LineData.Length > 7)
                {
                    No = LineData[0];
                    if (LineData[1] == "*")
                    {
                        Time += "* / ";
                        continue;
                    }
                    IP = LineData[1];
                    Time += LineData[3] + " / ";
                    Geolocation = LineData[5] + " " + LineData[6] + " " + LineData[7] + " " + LineData[8];
                    AS = LineData[4];
                    Hostname = LineData[2];
                    Organization = LineData[9];
                    Latitude = LineData[10];
                    Longitude = LineData[11];
                }
            }
            Time = Time[..^3]; // 去掉最末尾多余的东西

            // 匹配特定网络地址
            if (new Regex(@"^(127\.)|(192\.168\.)|(10\.)|(172\.1[6-9]\.)|(172\.2[0-9]\.)|(172\.3[0-1]\.)|(::1$)|([fF][cCdD])").IsMatch(IP))
            {
                Geolocation = "Private Address";
            }
            if (new Regex(@"^(100\.6[4-9]\.)|(100\.[7-9][0-9]\.)|(100\.1[0-1][0-9]\.)|(100\.12[0-7]\.)").IsMatch(IP))
            {
                Geolocation = "Shared Address";
            }
            if (new Regex(@"^(169\.254\.)").IsMatch(IP))
            {
                Geolocation = "Link-local Address";
            }
            if (new Regex(@"^(127\.)").IsMatch(IP))
            {
                Geolocation = "Loopback Address";
            }


            return new TracerouteResult(No, IP, Time, Geolocation, AS, Hostname, Organization, Latitude, Longitude);
        }
        public void Kill()
        {
            _process.Kill();
        }
    }
}