using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.Management;
using System.Linq;
using System.Collections.Generic;

namespace BluetoothConfig
{
    public partial class ConfigMainForm : Form
    {
        enum ParamType
        {
            None,
            Integer,
            String,
            QuotedString
        };
        class BluetoothParam
        {
            public String Label;
            public String Command;
            public String Replace;
            public ParamType Type;
            public String Request;
            public String Response;
            public String Status;
            public String Value;
        }

        private BluetoothParam[] paramList;

        SerialPort serialPort;

        static string ClearResponse(BluetoothParam param, bool fulldecode)
        {
            string response = param.Response;
            response = response.Replace(param.Command + ":", "");
            response = response.Replace("+", "");
            response = response.Replace("\"", "");

            if(!String.IsNullOrWhiteSpace(param.Replace))
            response = response.Replace(param.Replace + ":", "");

            if (fulldecode)
                response = response.Replace(":", ",");

            return response;
        }

        public ConfigMainForm()
        {
            InitializeComponent();

            paramList = new BluetoothParam[] {
#if DEBUG
                new BluetoothParam { Label = "Debug Fail", Command = "DEBUG", Type = ParamType.None },
#endif
                new BluetoothParam { Label = "State", Command = "STATE", Type = ParamType.None },
                new BluetoothParam { Label = "Version", Command = "VERSION", Type = ParamType.None },
                new BluetoothParam { Label = "Address", Command = "ADDR", Type = ParamType.None },
                new BluetoothParam { Label = "Name", Command = "NAME", Type = ParamType.QuotedString },
                new BluetoothParam { Label = "PIN", Command = "PSWD", Type = ParamType.QuotedString, Replace = "PIN" },
                new BluetoothParam { Label = "Speed", Command = "UART", Type = ParamType.String },
                new BluetoothParam { Label = "Role", Command = "ROLE", Type = ParamType.Integer },
                new BluetoothParam { Label = "Connection Mode", Command = "CMODE", Type = ParamType.Integer },
                new BluetoothParam { Label = "Binding Address", Command = "BIND", Type = ParamType.String }
            };
        }

        private void ConfigMainForm_Load(object sender, EventArgs e)
        {
            EnableButtons(false);
        }

        private void ButtonOpen_Click(object sender, EventArgs e)
        {
            if(cbCOM.SelectedItem == null) return;

            string nameCOM = cbCOM.SelectedItem.ToString();

            if(String.IsNullOrWhiteSpace(nameCOM)) return;

            nameCOM = nameCOM.Split(' ').FirstOrDefault();

            if (String.IsNullOrWhiteSpace(nameCOM)) return;

            try
            {
                serialPort = new SerialPort
                {
                    PortName = nameCOM,
                    BaudRate = 38400,
                    NewLine = "\r\n",
                    ReadTimeout = 100,
                    WriteTimeout = 100
                };

                serialPort.Open();

                EnableButtons(true);

                FillParamsList();

                ReadValues();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Error opening port", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void FillParamsList()
        {
            foreach (BluetoothParam param in paramList)
            {
                ListViewItem item = listParams.Items.Add(param.Label);

                item.Tag = param;
                item.UseItemStyleForSubItems = false;

                item.SubItems.Add("");
                item.SubItems.Add("");
                item.SubItems.Add("");
            }
        }

        private void ButtonClose_Click(object sender, EventArgs e)
        {
            if(serialPort != null)
            {
                serialPort.Close();
                serialPort = null;
            }

            listParams.Items.Clear();

            EnableButtons(false);
        }

        private void EnableButtons(bool bOpen)
        {
            cbCOM.Enabled = !bOpen;
            buttonOpen.Enabled = !bOpen;
            buttonClose.Enabled = bOpen;
            listParams.Enabled = bOpen;
            buttonRead.Enabled = bOpen;
            buttonWrite.Enabled = bOpen;
        }

        private void ComboCOM_DropDown(object sender, EventArgs e)
        {
            cbCOM.Items.Clear();

            using(ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM WIN32_SerialPort"))
            {
                string[] portnames = SerialPort.GetPortNames();
                List<ManagementBaseObject> ports = searcher.Get().Cast<ManagementBaseObject>().ToList();
                List<string> tList = (from n in portnames
                             join p in ports on n equals p["DeviceID"].ToString()
                             select n + " - " + p["Caption"]).ToList();

                cbCOM.Items.AddRange(tList.ToArray());
            }

            buttonOpen.Enabled = cbCOM.Items.Count > 0;
        }

        private void ButtonRead_Click(object sender, EventArgs e)
        {
            ReadValues();
        }

        void ReadValues()
        {
            serialPort.DiscardInBuffer();

            foreach(ListViewItem item in listParams.Items)
            {
                if(item.Tag is BluetoothParam param)
                {
                    param.Request = $"AT+{param.Command}?";
                    param.Response = String.Empty;
                    param.Status = String.Empty;
                    param.Value = String.Empty;

                    try
                    {
                        serialPort.WriteLine(param.Request);

                        param.Response = serialPort.ReadLine();
                        param.Status = serialPort.ReadLine();
                    }
                    catch(TimeoutException)
                    { }


                    if(param.Status == "OK")
                    {
                        param.Value = ClearResponse(param, true);

                        item.SubItems[3].BackColor = SystemColors.Window;
                        item.SubItems[3].ForeColor = SystemColors.WindowText;

                        if(param.Type != ParamType.None &&
                        item.SubItems[1].Text == item.SubItems[2].Text)
                        {
                            item.SubItems[2].Text = param.Value; 
                        }

                        item.SubItems[1].Text = ClearResponse(param, false);

                        item.SubItems[3].Text = param.Status;
                    }
                    else
                    {
                        item.SubItems[3].BackColor = Color.Red;
                        item.SubItems[3].ForeColor = Color.White;

                        item.SubItems[3].Text = param.Response;
                    }
                }
            }
        }

        private void ButtonWrite_Click(object sender, EventArgs e)
        {
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();

            foreach(ListViewItem item in listParams.Items)
            {
                if(item.Tag is BluetoothParam param)
                {
                    if(param.Type == ParamType.None) continue;

                    param.Request = $"AT+{param.Command}=";

                    if(param.Type == ParamType.QuotedString)
                        param.Request += $"\"{param.Value}\"";
                    else 
                        param.Request += param.Value;

                    param.Status = String.Empty;

                    try
                    {
                        serialPort.WriteLine(param.Request);

                        param.Status = serialPort.ReadLine();
                    }
                    catch(TimeoutException)
                    { }

                    item.SubItems[3].Text = param.Status;

                    if(param.Status == "OK")
                    {
                        item.SubItems[3].BackColor = Color.LightGreen;
                        item.SubItems[3].ForeColor = Color.Black;
                    }
                    else
                    {
                        item.SubItems[3].BackColor = Color.Red;
                        item.SubItems[3].ForeColor = Color.White;
                    }
                }
            }
        }
         
        private void listParams_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListViewItem item = listParams.GetItemAt(e.X, e.Y);
            if(item == null) return;

            if(item.Tag is BluetoothParam param && param.Type != ParamType.None)
            {
                ListViewItem.ListViewSubItem si = item.GetSubItemAt(e.X, e.Y);

                if(item.SubItems.IndexOf(si) == 2)
                {
                    TextBox box = new TextBox();

                    listParams.Controls.Add(box);

                    si.Tag = param;
                    box.Tag = item;
                    box.Bounds = si.Bounds;
                    box.Text = param.Value;
                    box.BringToFront();
                    box.Focus();

                    box.LostFocus += EditBox_LostFocus;
                    box.KeyPress += EditBox_KeyPress;
                }
            }
        }

        private void EditBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if(e.KeyChar == '\r' || e.KeyChar == '\n') EditBox_LostFocus(sender, e);
        }

        private void EditBox_LostFocus(object sender, EventArgs e)
        {
            TextBox box = sender as TextBox;
            ListViewItem item = box.Tag as ListViewItem;

            if(item.Tag is BluetoothParam param)
            {
                param.Value = box.Text;
                item.SubItems[2].Text = param.Value;
                item.SubItems[2].BackColor = SystemColors.Window;
                item.SubItems[2].ForeColor = SystemColors.WindowText;
            }

            listParams.Controls.Remove(box);
        }

        private void ButtonAll_Click(object sender, EventArgs e)
        {
            StringBuilder buffer = new StringBuilder();

            foreach(ListViewItem item in listParams.Items)
            {
                if(item.Tag is BluetoothParam param)
                {
                    buffer.AppendLine(param.Label + ": " + param.Value);
                }
            }

            Clipboard.SetText(buffer.ToString());
        }
    }
}
