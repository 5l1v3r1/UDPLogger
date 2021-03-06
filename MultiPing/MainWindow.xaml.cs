﻿/* UDPLogger 
   october 2015
   Amund Børsand 
*/

using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;

using System.Net;
using System.Net.Sockets;
using System.Windows.Threading;
using System.Reflection;
using OxyPlot.Wpf;
using OxyPlot.Axes;
using OxyPlot;
using Microsoft.Win32;
using System.IO;
using UDPLogger;
using System.Xml.Serialization;
using System.Linq;

namespace MultiPing {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  /// 


  public partial class MainWindow : Window {

    public static MainWindow mainWin;
    public static Dispatcher disp;
    public bool continous;

    // Our main data structure

    private ResultsCollection pingResults;

    // Public property, for the UI to access
    public ResultsCollection PingResults {
      get {
        return pingResults;
      }
    }

    UdpClient Client;

    public MainWindow() {
      pingResults = new ResultsCollection();

      InitializeComponent();

      System.Threading.Thread.CurrentThread.CurrentCulture =
        System.Globalization.CultureInfo.InvariantCulture;

      //PingList.ItemsSource = pingResults._collection;

      mainWin = this;
      disp = this.Dispatcher;

      Plot1.Axes.Add(new OxyPlot.Wpf.TimeSpanAxis());
      Plot1.Axes[0].Position = AxisPosition.Bottom;


      var linearAxis = new OxyPlot.Wpf.LinearAxis();
      //linearAxis.Title = "V";
      linearAxis.Key = "V";
      //linearAxis.PositionTier = 1;
      linearAxis.Position = AxisPosition.Left;
      Plot1.Axes.Add(linearAxis);

      // Plot1.Series[0].TrackerFormatString = "{2:0.0},{4:0.0}";

      /*linearAxis = new OxyPlot.Wpf.LinearAxis();
      linearAxis.Title = "RPM";
      linearAxis.Key = "RPM";
      linearAxis.PositionTier = 2;
      linearAxis.Position = AxisPosition.Right;
      Plot1.Axes.Add(linearAxis);*/

      /*linearAxis = new OxyPlot.Wpf.LinearAxis();
      linearAxis.Title = "mAh";
      linearAxis.Key = "mAh";
      linearAxis.PositionTier = 2;
      linearAxis.Position = AxisPosition.Right;
      Plot1.Axes.Add(linearAxis);

      linearAxis = new OxyPlot.Wpf.LinearAxis();
      linearAxis.Title = "Vtot";
      linearAxis.Key = "Vtot";
      linearAxis.PositionTier = 2;
      linearAxis.Position = AxisPosition.Left;
      Plot1.Axes.Add(linearAxis);

      linearAxis = new OxyPlot.Wpf.LinearAxis();
      linearAxis.Title = "C/A";
      linearAxis.Key = "Temp";
      linearAxis.PositionTier = 3;
      linearAxis.Position = AxisPosition.Left;
      Plot1.Axes.Add(linearAxis);*/

      //c_ThresholdReached += pingResults._collection.CollectionChanged;

      // Get version. Crashes if within visual studio, so we have to catch that.
      try {
        var version = System.Deployment.Application.ApplicationDeployment.CurrentDeployment.CurrentVersion;
        this.Title = "UDPLogger v." + version.ToString();
      } catch (Exception) {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        this.Title = "UDPLogger development build " + version.ToString();
      }
    }

    private void CallBack(IAsyncResult res) {
      IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 4444);
      byte[] received = Client.EndReceive(res, ref RemoteIpEndPoint);
      string s = Encoding.ASCII.GetString(received);
      
      Console.WriteLine(s);

      if (!s.Contains("garbage")) {
        string[] lines = s.Split('\n');
        foreach (var line in lines) {
          string[] split = line.Split(':');
          if (split.Length > 1)
            MainWindow.disp.BeginInvoke(DispatcherPriority.Normal,
             new Action(() => {
               double d = 0;
               d = Double.Parse(split[1]/*.Replace('.', ',')*/);
               PingResult temp = pingResults.Add(split[0], d);
               if (temp != null) {
                 Plot1.Series.Add(temp.Line);
                 temp.Line.ItemsSource = temp.Points;
               }
               Plot1.InvalidatePlot(true);
             }));
        }
      }

      if (continous)
        Client.BeginReceive(new AsyncCallback(CallBack), null);
    }

    public void EnableDisableSeries(PingResult p,bool enable) {
      if (enable) {
        if (!Plot1.Series.Contains(p.Line))
          Plot1.Series.Add(p.Line);
        p.Line.ItemsSource = p.Points;
      } else
        Plot1.Series.Remove(p.Line);
      Plot1.InvalidatePlot(true);
    }

    private void PingButton_Click(object sender, RoutedEventArgs e) {
      try { 
      //PingButton.IsEnabled = !continuous;
      continous = !continous;
      if (continous)
        PingButton.Content = "Stop";
      else
        PingButton.Content = "Listen";

      if (!continous) {
        return;
      }

      if (Client == null)
        Client = new UdpClient(4444);

      //Creates an IPEndPoint to record the IP Address and port number of the sender.
      // The IPEndPoint will allow you to read datagrams sent from any source.
      IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

      Client.BeginReceive(new AsyncCallback(CallBack), null);
      } catch (Exception ex) {
        MessageBox.Show(ex.ToString());
      }

    }

    private void SaveButton_Click(object sender, RoutedEventArgs e) {
      try {
        SaveFileDialog save = new SaveFileDialog();
        save.Filter = "Log|*.log";
        if ((bool)save.ShowDialog()) {
          FileStream output = new FileStream(save.FileName, FileMode.Create);
          XmlSerializer x = new XmlSerializer(pingResults.GetType());
          x.Serialize(output, pingResults);
          output.Close();
        }
      } catch (Exception ex) {
        MessageBox.Show(ex.ToString());
      }
    }

    private void CheckBox_Click_1(object sender, RoutedEventArgs e) {
      continous = (bool)((CheckBox)sender).IsChecked;
      if (!continous) PingButton.IsEnabled = true;
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e) {
      try {
        OpenFileDialog load = new OpenFileDialog();
        load.Filter = "CSV|*.csv|Log|*.log";
        string title = "";
        if ((bool)load.ShowDialog()) {

          //Plot1.ResetAllAxes();
          Plot1.Series.Clear();
          foreach (var p in pingResults._collection) 
            p.Points.Clear();
           
          pingResults._collection.Clear();

          if (load.FilterIndex == 2) {

            XmlSerializer mySerializer = new XmlSerializer(typeof(ResultsCollection));
            FileStream myFileStream = new FileStream(load.FileName, FileMode.Open);
            // Call the Deserialize method and cast to the object type.
            pingResults = (ResultsCollection)mySerializer.Deserialize(myFileStream);

          } else {

            var f = File.OpenRead(load.FileName);
            var stream = new StreamReader(f);
            string header = stream.ReadLine();
            while (header.TrimStart().StartsWith("//")) {
              title += header;
              header = stream.ReadLine(); // VESC MOnitor first line is a summary/comment
            }
            var columns = header.Split(',');
            int columnCount = columns.Count();
            int lineNumber = 0;
            int hasTime = -1;
            if (columns[0].ToUpper().Contains("TIME"))
              hasTime = 0;
            if (columns.Contains("TimePassedInMs"))
              hasTime = Array.IndexOf(columns, "TimePassedInMs");
            while (!stream.EndOfStream) {
              var line = stream.ReadLine();
              int i = 0;
              int t = 0;

              if (hasTime != -1) {
                double d;
                double.TryParse(line.Split(',')[hasTime], out d);
                t = (int)d;
              }

              foreach (var c in line.Split(',')) {
                double d = 0;

                if (i == hasTime) {
                  i++;
                  continue; // don't graph the timestamp
                }

                if (double.TryParse(c, out d)) {
                  PingResult temp;
                  if (hasTime != -1)
                    temp = pingResults.Add(columns[i], d, t / 1000.0);
                  else
                    temp = pingResults.Add(columns[i], d, lineNumber++);
                  if (temp != null) {
                    Plot1.Series.Add(temp.Line);
                    temp.Line.ItemsSource = temp.Points;
                  }

                }
                i++;
              }
              //if (lineNumber % columnCount==0)
              //Plot1.InvalidatePlot(true);
            }

          }


          /*foreach (var p in pingResults._collection)
            EnableDisableSeries(p, true);*/

          Plot1.InvalidatePlot(true);
          this.Title = Path.GetFileName(load.FileName) + title;
        }
      } catch (Exception ex) {
        MessageBox.Show(ex.ToString());
      }
    }

    private void Items_Click(object sender, RoutedEventArgs e) {
      ItemsWindow i = new ItemsWindow(pingResults);
    }

    void c_ThresholdReached(object sender, EventArgs e) {
      Console.WriteLine("The threshold was reached.");
    }

    private void button_Click(object sender, RoutedEventArgs e) {
      foreach (var axe in Plot1.Axes)
        axe.InternalAxis.Reset();

      Plot1.InvalidatePlot();
    }

    private void Plot1_MouseDown_1(object sender, System.Windows.Input.MouseEventArgs e) {
      {
        //Console.Write(e.LeftButton);
        ///if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
          return;
        var axisList = Plot1.Axes;
        OxyPlot.Wpf.Axis X_Axis = null;
        OxyPlot.Wpf.Axis Y_Axis = null;
        foreach (var ax in axisList) {
          if (ax.Position == AxisPosition.Bottom)
            X_Axis = ax;
          else if (ax.Position == AxisPosition.Left)
            Y_Axis = ax;
        }
        var point = e.GetPosition(this);
        Console.WriteLine(point.X + " " + point.Y + " min " + X_Axis.Minimum+ " max " + X_Axis.Maximum);

        double timestamp = point.X;

        if (pingResults._collection.Count > 0)
          foreach (var p in pingResults._collection)
            p.SetLegendValue(p.Points
                              .Where(x => x.X >= timestamp*1000)
                              .FirstOrDefault()
                              .Y);
        //DataPoint p = .InverseTransform(point.X);
      }
    }

  }
}
