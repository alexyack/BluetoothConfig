﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;

namespace BluetoothConfig
{
    public partial class ConfigMainForm : Form
    {
        enum ParamType
        {
            None,
            Boolean,
            Integer,
            String
        };
        struct BluetoothParam
        {
            public String Label;
            public String Command;
            public ParamType Type;
            public int ValueMin, ValueMax;
        }

        private readonly BluetoothParam[] paramList = new BluetoothParam[] {
            new BluetoothParam { Label = "Dummy", Command = "QQQQ", Type = ParamType.None },
            new BluetoothParam { Label = "Version", Command = "VERSION", Type = ParamType.None },
            new BluetoothParam { Label = "Address", Command = "ADDR", Type = ParamType.None },
            new BluetoothParam { Label = "Name", Command = "NAME", Type = ParamType.String },
            new BluetoothParam { Label = "PIN", Command = "PSWD", Type = ParamType.String },
            new BluetoothParam { Label = "Speed", Command = "UART", Type = ParamType.String },
            new BluetoothParam { Label = "Role", Command = "ROLE", Type = ParamType.Integer, ValueMin = 0, ValueMax = 2 },
            new BluetoothParam { Label = "Connection Mode", Command = "CMODE", Type = ParamType.Integer, ValueMin = 0, ValueMax = 1 },
            new BluetoothParam { Label = "Binding Address", Command = "BIND", Type = ParamType.String }
        };

        SerialPort serialPort;

        static string ClearResponse(BluetoothParam param, string response)
        {
            response = response.Replace(param.Command + ":", "");
            response = response.Replace("PIN:", "");
            response = response.Replace("+", "");
            response = response.Replace("\"", "");
//            response = response.Replace(":", ",");

            return response;
        }

        public ConfigMainForm()
        {
            InitializeComponent();
        }

        private void ConfigMainForm_Load(object sender, EventArgs e)
        {
            EnableButtons(false);
        }

        private void ButtonOpen_Click(object sender, EventArgs e)
        {
            if (cbCOM.SelectedItem == null) return;

            string nameCOM = cbCOM.SelectedItem.ToString();

            if (String.IsNullOrWhiteSpace(nameCOM)) return;

            try
            {
                serialPort = new SerialPort
                {
                    PortName = cbCOM.SelectedItem.ToString(),
                    BaudRate = 38400,
                    NewLine = "\r\n",
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };

                serialPort.Open();

                EnableButtons(true);
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.Message, "Error opening port", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error opening port", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            FillParamsList();
        }

        void FillParamsList()
        {
            foreach (BluetoothParam param in paramList)
            {
                ListViewItem item = listParams.Items.Add(param.Label);
                item.Tag = param;

                item.SubItems.Add(" ");
                item.SubItems.Add(" ");
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
            cbCOM.Items.AddRange(SerialPort.GetPortNames());
        }

        private void ButtonRead_Click(object sender, EventArgs e)
        {
            string request;
            string response, confirm;

            serialPort.DiscardInBuffer();

            foreach(ListViewItem item in listParams.Items)
            {
                if(item.Tag is BluetoothParam param)
                {
                    request = "AT+" + param.Command + "?";

                    response = String.Empty;
                    confirm = String.Empty;

                    try
                    {
                        serialPort.WriteLine(request);

                        response = serialPort.ReadLine();
                        confirm = serialPort.ReadLine();
                    }
                    catch (TimeoutException ex)
                    { }

                    if (confirm == "OK")
                    {
                        item.BackColor = SystemColors.Window;
                        item.ForeColor = SystemColors.WindowText;

                        response = ClearResponse(param, response);

                        if (param.Type != ParamType.None &&
                        item.SubItems[1].Text == item.SubItems[2].Text)
                            item.SubItems[2].Text = response;

                        item.SubItems[1].Text = response;
                    }
                    else
                    {
                        item.BackColor = Color.Red;
                        item.ForeColor = Color.White;

                        item.SubItems[1].Text = response;

                        item.Tag = null;
                    }
                }
            }
        }

        private void ButtonWrite_Click(object sender, EventArgs e)
        {

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

                    box.Tag = si;
                    box.Bounds = si.Bounds;
                    box.Text = si.Text;
                    box.BringToFront();
                    box.Focus();

                    box.LostFocus += EditBox_LostFocus;
                    box.KeyPress += EditBox_KeyPress;
                }
            }
        }

        private void EditBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r') EditBox_LostFocus(sender, e);
        }

        private void EditBox_LostFocus(object sender, EventArgs e)
        {
            TextBox box = sender as TextBox;
            ListViewItem.ListViewSubItem si = box.Tag as ListViewItem.ListViewSubItem;

            si.Text = box.Text;

            listParams.Controls.Remove(box);
        }
    }
}
