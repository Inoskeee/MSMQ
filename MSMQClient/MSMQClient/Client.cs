using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Messaging;
using System.Threading;

namespace MSMQ
{
    public partial class frmMain : Form
    {
        private MessageQueue q = null;      // очередь сообщений, в которую будет производиться запись сообщений

        private MessageQueue receiveQueue = null;      // очередь сообщений, из которой будет производиться чтение сообщений

        private Thread t = null;                // поток, отвечающий за работу с очередью сообщений
        private bool _continue = true;          // флаг, указывающий продолжается ли работа с мэйлслотом
        // конструктор формы
        public frmMain()
        {
            InitializeComponent();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (MessageQueue.Exists(tbPath.Text))
            {
                if (!string.IsNullOrEmpty(userLogin.Text))
                {
                    string path = Dns.GetHostName() + $"\\private$\\{userLogin.Text}";    // путь к очереди сообщений, Dns.GetHostName() - метод, возвращающий имя текущей машины

                    if (!MessageQueue.Exists(path))
                    {
                        // если очередь, путь к которой указан в поле tbPath существует, то открываем ее
                        q = new MessageQueue(tbPath.Text);

                        receiveQueue =  MessageQueue.Create(path);
                        receiveQueue.Formatter = new XmlMessageFormatter(new Type[] { typeof(String) });

                        btnSend.Enabled = true;
                        btnConnect.Enabled = false;
                        userLogin.Enabled = false;

                        q.Send("connected", userLogin.Text);
                        // создание потока, отвечающего за работу с очередью сообщений
                        t = new Thread(ReceiveMessage);
                        t.Start();
                    }
                    else
                    {
                        MessageBox.Show("Такой пользователь уже есть в чате");
                    }
                }
                else
                {
                    MessageBox.Show("Логин пользователя не указан");
                }
            }
            else
                MessageBox.Show("Указан неверный путь к очереди, либо очередь не существует");
        }


        // получение сообщения
        private void ReceiveMessage()
        {
            if (receiveQueue == null)
                return;

            System.Messaging.Message msg = null;
            try
            {
                // входим в бесконечный цикл работы с очередью сообщений
                while (_continue)
                {
                    if (receiveQueue != null && receiveQueue.Peek() != null)   // если в очереди есть сообщение, выполняем его чтение, интервал до следующей попытки чтения равен 10 секундам
                        msg = receiveQueue.Receive(TimeSpan.FromSeconds(10.0));

                    messageBox.Invoke((MethodInvoker)delegate
                    {
                        if (msg != null)
                            messageBox.Text += "\n >> " + msg.Label + " : " + msg.Body;     // выводим полученное сообщение на форму
                    });
                    Thread.Sleep(500);          // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Сервер перестал отвечать");
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            try
            {
                // выполняем отправку сообщения в очередь
                q.Send(tbMessage.Text, userLogin.Text);
            }
            catch(Exception ex)
            {
                MessageBox.Show("Сервер больше недоступен. Перезапустите приложение");
            }
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _continue = false;      // сообщаем, что работа с очередью сообщений завершена

            if (t != null)
            {
                t.Abort();          // завершаем поток
            }

            if (receiveQueue != null)
            {
                //MessageQueue.Delete(receiveQueue.Path);      // в случае необходимости удаляем очередь сообщений
            }
        }
    }
}