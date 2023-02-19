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
using System.Threading;
using System.Messaging;

namespace MSMQ
{
    public partial class frmMain : Form
    {
        private MessageQueue q = null;          // очередь сообщений
        private Thread t = null;                // поток, отвечающий за работу с очередью сообщений
        private bool _continue = true;          // флаг, указывающий продолжается ли работа с мэйлслотом

        private List<string> queues = new List<string>();
        // конструктор формы
        public frmMain()
        {
            InitializeComponent();
            string path = Dns.GetHostName() + "\\private$\\ServerQueue";    // путь к очереди сообщений, Dns.GetHostName() - метод, возвращающий имя текущей машины

            // если очередь сообщений с указанным путем существует, то открываем ее, иначе создаем новую
            if (MessageQueue.Exists(path))
                q = new MessageQueue(path);
            else
                q = MessageQueue.Create(path);

            // задаем форматтер сообщений в очереди
            q.Formatter = new XmlMessageFormatter(new Type[] { typeof(String) });

            // вывод пути к очереди сообщений в заголовок формы, чтобы можно было его использовать для ввода имени в форме клиента, запущенного на другом вычислительном узле
            this.Text += "     " + q.Path;

            // создание потока, отвечающего за работу с очередью сообщений
            t = new Thread(ReceiveMessage);
            t.Start();
        }

        // получение сообщения
        private void ReceiveMessage()
        {
            if (q == null)
                return;

            System.Messaging.Message msg = null;

            // входим в бесконечный цикл работы с очередью сообщений
            while (_continue)
            {
                if (q.Peek() != null)   // если в очереди есть сообщение, выполняем его чтение, интервал до следующей попытки чтения равен 10 секундам
                    msg = q.Receive(TimeSpan.FromSeconds(10.0));

                if(!queues.Contains(msg.Label) && msg.Body.ToString() == "connected")
                {
                    var sender = msg.Label;
                    queues.Add(sender);
                    msg.Label = "\0";
                    rtbMessages.Invoke((MethodInvoker)delegate
                    {
                        if (!string.IsNullOrEmpty(rtbMessages.Text))
                        {
                            msg.Body = rtbMessages.Text;
                        }
                        else
                        {
                            msg.Body = "Пока нет сообщений в чате";
                        }
                    });

                    string path = Dns.GetHostName() + $"\\private$\\{sender}";    // путь к очереди сообщений, Dns.GetHostName() - метод, возвращающий имя текущей машины

                    MessageQueue queue;
                    // если очередь сообщений с указанным путем существует, то открываем ее, иначе создаем новую
                    if (MessageQueue.Exists(path))
                        queue = new MessageQueue(path);
                    else
                        queue = MessageQueue.Create(path);

                    queue.Send(msg.Body, msg.Label);
                }
                else
                {
                    rtbMessages.Invoke((MethodInvoker)delegate
                    {
                        if (msg != null)
                            rtbMessages.Text += "\n >> " + msg.Label + " : " + msg.Body;     // выводим полученное сообщение на форму
                    });

                    if (queues.Contains(msg.Label))
                    {
                        SendMessages(msg);
                    }
                    else
                    {
                        queues.Add(msg.Label);
                        SendMessages(msg);
                    }
                }

                Thread.Sleep(500);          // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
            }
        }

        private void SendMessages(System.Messaging.Message msg)
        {
            var tempList = new List<string>();
            foreach(var item in queues)
            {
                string path = Dns.GetHostName() + $"\\private$\\{item}";    // путь к очереди сообщений, Dns.GetHostName() - метод, возвращающий имя текущей машины

                if (MessageQueue.Exists(path))
                    tempList.Add(item);
            }

            queues = tempList;

            foreach (var item in queues)
            {
                string path = Dns.GetHostName() + $"\\private$\\{item}";    // путь к очереди сообщений, Dns.GetHostName() - метод, возвращающий имя текущей машины

                MessageQueue queue;
                // если очередь сообщений с указанным путем существует, то открываем ее, иначе создаем новую
                if (MessageQueue.Exists(path))
                    queue = new MessageQueue(path);
                else
                    queue = MessageQueue.Create(path);

                queue.Send(msg.Body, msg.Label);
            }
        }


        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _continue = false;      // сообщаем, что работа с очередью сообщений завершена

            if (t != null)
            {
                t.Abort();          // завершаем поток
            }

            if (q != null)
            {
                MessageQueue.Delete(q.Path);      // в случае необходимости удаляем очередь сообщений
            }
            if(queues.Count > 0)
            {
                foreach(var item in queues)
                {
                    string path = Dns.GetHostName() + $"\\private$\\{item}";
                    try
                    {
                        MessageQueue.Delete(path);
                    }
                    catch(Exception ex) { }
                }
            }
        }
    }
}