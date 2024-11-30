using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace NatTraversal {
    public partial class Form1 : Form {
        private List<string> lines = new List<string>();
        private WanUDPCom udpCom = new WanUDPCom();

        public Form1() {
            InitializeComponent();
            udpCom.revMessageCallback = (str) => {
                this.Invoke(delegate () {
                    writeToTextbox(str);
                });
            };
            udpCom.relayStopCallback = () => {
                this.Invoke(delegate () {
                    resetBtnStartText();
                });
            };
        }

        private void writeToTextbox(string str) {
            lines.Add(str);
            tbOut.Lines = lines.ToArray();
        }

        private void btnSend_Click(object sender, EventArgs e) {
            string text = tbMessage.Text;
            if (text.Length == 0)
                return;
            udpCom.SendMessage(text);
            writeToTextbox("[local]" + text);
        }

        private void btnStart_Click(object sender, EventArgs e) {
            tbRevPort.ReadOnly = !tbRevPort.ReadOnly;
            rbClient.Enabled = !rbClient.Enabled;
            rbServer.Enabled = !rbServer.Enabled;
            btnConfirm.Enabled = !btnConfirm.Enabled;
            if (tbRevPort.ReadOnly) {//�]���J�n
                btnStart.Text = "�]����~";
                try {
                    int port = int.Parse(tbRevPort.Text);
                    if (1024 <= port && port<=65535) {
                        udpCom.StartRelay(rbServer.Checked, port);
                    } else {
                        writeToTextbox("1024 - 65535�͈̔͂Ń|�[�g�ԍ���ݒ肵�Ă�������");
                    }
                }catch (FormatException ex) {
                    writeToTextbox("�|�[�g�ԍ�������������܂���");
                    Debug.WriteLine(ex.Message);
                }
            } else {//�]����~
                btnStart.Text = "�]���J�n";
                udpCom.StopRelay();
            }
        }
        private void resetBtnStartText() {
            tbRevPort.ReadOnly = !tbRevPort.ReadOnly;
            rbClient.Enabled = !rbClient.Enabled;
            rbServer.Enabled = !rbServer.Enabled;
            btnConfirm.Enabled = !btnConfirm.Enabled;
            btnStart.Text = "�]���J�n";
        }

        private void btnSTUN_Click(object sender, EventArgs e) {
            udpCom.SendStunRequest((endpoint) => {
                this.Invoke(delegate () {
                    writeToTextbox(endpoint.ToString());
                });
            });

        }

        private void btnConfirm_Click(object sender, EventArgs e) {
            tbSendIP.ReadOnly = !tbSendIP.ReadOnly;
            tbSendPort.ReadOnly = !tbSendPort.ReadOnly;
            btnStart.Enabled = !btnStart.Enabled;
            btnSend.Enabled = !btnSend.Enabled;
            btnSTUN.Enabled = !btnSTUN.Enabled;
            if (tbSendIP.ReadOnly) {//�m����
                btnConfirm.Text = "�L�����Z��";
                IPEndPoint ep;
                try {
                    ep = new IPEndPoint(IPAddress.Parse(tbSendIP.Text), int.Parse(tbSendPort.Text));
                    udpCom.SendEp = ep;
                } catch (SystemException ex) when (ex is FormatException || ex is OverflowException) {
                    Debug.WriteLine(ex.Message);
                    return;
                }
                udpCom.StartReceive();
            } else {
                btnConfirm.Text = "�m��";
                udpCom.StopReceive();
            }

        }
    }
}