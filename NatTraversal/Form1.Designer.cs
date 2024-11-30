namespace NatTraversal {
    partial class Form1 {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            btnSend = new Button();
            tbRevPort = new TextBox();
            labRevPort = new Label();
            labSendIP = new Label();
            tbSendIP = new TextBox();
            labSendPort = new Label();
            tbSendPort = new TextBox();
            tbMessage = new TextBox();
            tbOut = new TextBox();
            btnStart = new Button();
            btnSTUN = new Button();
            rbClient = new RadioButton();
            rbServer = new RadioButton();
            lbMode = new Label();
            btnConfirm = new Button();
            SuspendLayout();
            // 
            // btnSend
            // 
            btnSend.Enabled = false;
            btnSend.Location = new Point(178, 194);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(75, 23);
            btnSend.TabIndex = 0;
            btnSend.Text = "送信";
            btnSend.UseVisualStyleBackColor = true;
            btnSend.Click += btnSend_Click;
            // 
            // tbRevPort
            // 
            tbRevPort.Location = new Point(152, 234);
            tbRevPort.Name = "tbRevPort";
            tbRevPort.Size = new Size(100, 23);
            tbRevPort.TabIndex = 1;
            tbRevPort.Text = "25565";
            tbRevPort.TextAlign = HorizontalAlignment.Right;
            // 
            // labRevPort
            // 
            labRevPort.AutoSize = true;
            labRevPort.Location = new Point(13, 242);
            labRevPort.Name = "labRevPort";
            labRevPort.Size = new Size(81, 15);
            labRevPort.TabIndex = 2;
            labRevPort.Text = "受信ポート番号";
            // 
            // labSendIP
            // 
            labSendIP.AutoSize = true;
            labSendIP.Location = new Point(13, 271);
            labSendIP.Name = "labSendIP";
            labSendIP.Size = new Size(53, 15);
            labSendIP.TabIndex = 3;
            labSendIP.Text = "送信先IP";
            // 
            // tbSendIP
            // 
            tbSendIP.Location = new Point(152, 263);
            tbSendIP.Name = "tbSendIP";
            tbSendIP.Size = new Size(100, 23);
            tbSendIP.TabIndex = 4;
            tbSendIP.TextAlign = HorizontalAlignment.Right;
            // 
            // labSendPort
            // 
            labSendPort.AutoSize = true;
            labSendPort.Location = new Point(13, 295);
            labSendPort.Name = "labSendPort";
            labSendPort.Size = new Size(81, 15);
            labSendPort.TabIndex = 5;
            labSendPort.Text = "送信ポート番号";
            // 
            // tbSendPort
            // 
            tbSendPort.Location = new Point(152, 292);
            tbSendPort.Name = "tbSendPort";
            tbSendPort.Size = new Size(100, 23);
            tbSendPort.TabIndex = 6;
            tbSendPort.TextAlign = HorizontalAlignment.Right;
            // 
            // tbMessage
            // 
            tbMessage.Location = new Point(13, 195);
            tbMessage.Name = "tbMessage";
            tbMessage.Size = new Size(159, 23);
            tbMessage.TabIndex = 7;
            // 
            // tbOut
            // 
            tbOut.AcceptsTab = true;
            tbOut.Location = new Point(12, 40);
            tbOut.Multiline = true;
            tbOut.Name = "tbOut";
            tbOut.ReadOnly = true;
            tbOut.ScrollBars = ScrollBars.Vertical;
            tbOut.Size = new Size(240, 148);
            tbOut.TabIndex = 9;
            // 
            // btnStart
            // 
            btnStart.Enabled = false;
            btnStart.Location = new Point(177, 375);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(75, 23);
            btnStart.TabIndex = 10;
            btnStart.Text = "転送開始";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // btnSTUN
            // 
            btnSTUN.Location = new Point(178, 12);
            btnSTUN.Name = "btnSTUN";
            btnSTUN.Size = new Size(75, 23);
            btnSTUN.TabIndex = 11;
            btnSTUN.Text = "STUN";
            btnSTUN.UseVisualStyleBackColor = true;
            btnSTUN.Click += btnSTUN_Click;
            // 
            // rbClient
            // 
            rbClient.AutoSize = true;
            rbClient.Checked = true;
            rbClient.Location = new Point(152, 321);
            rbClient.Name = "rbClient";
            rbClient.Size = new Size(55, 19);
            rbClient.TabIndex = 12;
            rbClient.TabStop = true;
            rbClient.Text = "Client";
            rbClient.UseVisualStyleBackColor = true;
            // 
            // rbServer
            // 
            rbServer.AutoSize = true;
            rbServer.Location = new Point(152, 346);
            rbServer.Name = "rbServer";
            rbServer.Size = new Size(57, 19);
            rbServer.TabIndex = 13;
            rbServer.Text = "Server";
            rbServer.UseVisualStyleBackColor = true;
            // 
            // lbMode
            // 
            lbMode.AutoSize = true;
            lbMode.Location = new Point(13, 325);
            lbMode.Name = "lbMode";
            lbMode.Size = new Size(56, 15);
            lbMode.TabIndex = 14;
            lbMode.Text = "動作モード";
            // 
            // btnConfirm
            // 
            btnConfirm.Location = new Point(96, 375);
            btnConfirm.Name = "btnConfirm";
            btnConfirm.Size = new Size(75, 23);
            btnConfirm.TabIndex = 15;
            btnConfirm.Text = "確定";
            btnConfirm.UseVisualStyleBackColor = true;
            btnConfirm.Click += btnConfirm_Click;
            // 
            // Form1
            // 
            AccessibleRole = AccessibleRole.TitleBar;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(268, 410);
            Controls.Add(btnConfirm);
            Controls.Add(lbMode);
            Controls.Add(rbServer);
            Controls.Add(rbClient);
            Controls.Add(btnSTUN);
            Controls.Add(btnStart);
            Controls.Add(tbOut);
            Controls.Add(tbMessage);
            Controls.Add(tbSendPort);
            Controls.Add(labSendPort);
            Controls.Add(tbSendIP);
            Controls.Add(labSendIP);
            Controls.Add(labRevPort);
            Controls.Add(tbRevPort);
            Controls.Add(btnSend);
            Name = "Form1";
            Text = "NatTraversal";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnSend;
        private TextBox tbRevPort;
        private Label labRevPort;
        private Label labSendIP;
        private TextBox tbSendIP;
        private Label labSendPort;
        private TextBox tbSendPort;
        private TextBox tbMessage;
        private Label labMessage;
        private TextBox tbOut;
        private Button btnStart;
        private Button btnSTUN;
        private RadioButton rbClient;
        private RadioButton rbServer;
        private Label lbMode;
        private Button btnConfirm;
    }
}