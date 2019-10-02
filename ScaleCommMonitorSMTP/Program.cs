using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Data.SqlClient;
using System.ServiceProcess;


// Create config file (xml) or ini file containing:
// - DB credentials
// - Service Name
// - IP addresses/hosts

using System.Net.Mail;



class TCPListener
{
    private static string ConnectDB()
    {

        // Build connection string
        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
        builder.DataSource = "webpvsql01";   // update me
        builder.UserID = "sa";              // update me
        builder.Password = "W3bst3r1";      // update me
        builder.InitialCatalog = "Warehouse";

        // Connect to SQL
        Console.Write("Connecting to SQL Server ... ");

        string connect = builder.ConnectionString;


        using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
        {
            connection.Open();
            Console.WriteLine(builder.ConnectionString);
            Console.WriteLine("Done.");
        }
        return connect;

    }

    private static void SendEmail(string emailSubject, string emailBody)
    {
        try
        {
            MailMessage mail = new MailMessage();
            SmtpClient SmtpServer = new SmtpClient("au-smtp-outbound-2.mimecast.com");

            mail.From = new MailAddress("email@websterltd.com.au");
            mail.To.Add("logs@websterltd.com.au");
            mail.Subject = emailSubject;
            mail.Body = emailBody;

            SmtpServer.Port = 587;
            SmtpServer.Credentials = new System.Net.NetworkCredential("email@websterltd.com.au", "Bamu8129");
            SmtpServer.EnableSsl = true;

            SmtpServer.Send(mail);
            Console.WriteLine("E-Mail Sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    // https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-open-and-append-to-a-log-file
    public static void Log(string logMessage, TextWriter w)
    {
       // w.Write("\r\nLog Entry : ");
        w.WriteLine($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()} {logMessage}");

    }
    public static void DumpLog(StreamReader r)
    {
        string line;
        while ((line = r.ReadLine()) != null)
        {
            Console.WriteLine(line);
        }
    }
    private static void WriteDB(string connectionString, string devName, string cmd, int netWeight)
    {
        //string queryString = "IF NOT EXISTS (SELECT * FROM dbo.web_ScaleCommData WHERE DevName = @devName) INSERT INTO dbo.web_ScaleCommData(ID,DevName,CMD,NetWeight,UOM) VALUES(1, devName, cmd, netWeight) ELSE UPDATE dbo.web_ScaleCommData SET cmd = @cmd, NetWeight = @netWeight WHERE DevName = @SS";
        // string queryString = "INSERT INTO dbo.web_ScaleCommData(ID,DevName,CMD,NetWeight,UOM) VALUES(, @devName, @cmd, @netWeight)";
        // string queryString = "INSERT into dbo.web_ScaleCommData(ID, DevName, CMD, NetWeight, UOM) VALUES(4, 'NM', 'TEST', 1400, 'KG');" ;
        //string queryString = "UPDATE dbo.web_ScaleCommData set DevName=@devName, CMD=@cmd, NetWeight=@netWeight, UOM='KG' WHERE DevName=@devName;";
      
        // string queryString = "IF EXISTS (SELECT * FROM dbo.web_ScaleWeights WHERE DevName = @devName) UPDATE dbo.web_ScaleWeights set DevName=@devName, CMD=@cmd, NetWeight=@netWeight, UOM='KG' WHERE DevName=@devName ELSE INSERT into dbo.web_ScaleWeights(DevName, CMD, NetWeight, UOM) VALUES(@devName, @cmd, @netWeight, 'KG');";
        string queryString = "IF EXISTS (SELECT * FROM dbo.web_ScaleWeights WHERE DevName = @devName) UPDATE dbo.web_ScaleWeights set DevName=@devName, CMD=@cmd, NetWeight=@netWeight, UOM='KG', DateTime=@dateTime WHERE DevName=@devName ELSE INSERT into dbo.web_ScaleWeights(DevName, CMD, NetWeight, UOM, DateTime) VALUES(@devName, @cmd, @netWeight, 'KG', @dateTime);";
        DateTime now = DateTime.Now;
        // "SELECT ID, DevName, CMD, NetWeight, UOM FROM dbo.web_ScaleCommData;";
        using (SqlConnection connection = new SqlConnection(
                   connectionString))
        {
            SqlCommand command = new SqlCommand(
                queryString, connection);
            command.Parameters.AddWithValue("@devName", devName);
            command.Parameters.AddWithValue("@cmd", cmd);
            command.Parameters.AddWithValue("@netWeight", netWeight);
            command.Parameters.AddWithValue("@dateTime", now);
            connection.Open();
            command.ExecuteNonQuery();

        }
    }



    private static string ConnectPort(string host, int port)
    {
        // Command line credentials
        //string restartComm = "net use \\kernelwrapper Sailoud3 /user:serviceadmin && SC \\kernelwrapper Stop PmxLogexScaleComm && SC \\kernelwrapper Start PmxLogexScaleComm";
        string serviceName = "PmxLogexScaleComm";
        //string username = "serviceadmin"; // remote username  
        //string password = "Sailoud3"; // remote password  
        try
        {
            // Initialise Socket Module
            byte[] receiveBytes = new byte[200];

            IPAddress ip = IPAddress.Parse(host);
            IPEndPoint ipe = new IPEndPoint(ip, port);//Convert ip and port to IPEndPoint instance
            Console.WriteLine("Starting Creating Socket Object");
            Socket sender = new Socket(AddressFamily.InterNetwork,
                                        SocketType.Stream,
                                        ProtocolType.Tcp);  // Create Socket 
                                                            // sender.SendTimeout = 5;
            sender.ReceiveTimeout = 2000;       //ms
                                                // http://www.csharp-examples.net/socket-send-receive/


            sender.Connect(ipe);    //Connect to Server 


            // End Socket Module
            //Console.WriteLine("Connected");
            int totalBytesReceived = sender.Receive(receiveBytes);      // skip first set of data received
            totalBytesReceived = sender.Receive(receiveBytes);          //receive data

            // Console.WriteLine(totalBytesReceived);
            string txt = Encoding.ASCII.GetString(receiveBytes, 0, totalBytesReceived); //write variable to string
            Console.WriteLine(txt);

            sender.Shutdown(SocketShutdown.Both);
            sender.Close();
            return txt;
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            string errorMsg = ex.Message;
            Console.WriteLine(ex.Message);
            if (errorMsg.Contains("A connection attempt failed"))
            {
                string errorHost = "???<dev name=" + host + "cmd=event:IP-OFFLINE net=0 uom= />";
                //SendEmail("ScaleComm Host: " + host + " Offline", errorHost);     //disabled so constant e-mails aren't sent when computer is turned off
                return errorHost;
            }
            else
            {
                string errorHost = "???<dev name=" + host + "cmd=event:SERVICE-FAILED net=0 uom= />";
                string errorStatus = "???<dev name=" + host + "cmd=event:SERVICE-FAILED-RESTARTING net=0 uom= />";

                // Restart Scale Comm service if it fails
                ServiceController sc = new ServiceController(serviceName, host);

                //check if service is running, if so, stop then start, else start
                // randygray.com/method-to-start-a-windows-service-in-c/
                // stackoverflow.com/questions/178147/how-can-i-verify-if-a-windows-service-is-running

                switch (sc.Status)
                {
                    case ServiceControllerStatus.Running:
                        sc.Stop();
                        sc.Start();
                        SendEmail("ScaleComm Host: " + host + " Restarted", errorStatus);
                        using (StreamWriter w = File.AppendText("C:\\Produmex\\Log\\ScaleCommLog.txt"))
                        {
                            Log("ScaleComm Host: " + host + " Restarted", w);
                        }
                            return errorStatus;
                    case ServiceControllerStatus.Stopped:
                        sc.Start();
                        SendEmail("ScaleComm Host: " + host + " Service stopped. Initiated Restart", errorStatus);
                        using (StreamWriter w = File.AppendText("C:\\Produmex\\Log\\ScaleCommLog.txt"))
                        {
                            Log("ScaleComm Host: " + host + " Service stopped. Initiated Restart", w);
                        }
                        return errorStatus;
                    case ServiceControllerStatus.Paused:
                        return errorHost;
                    case ServiceControllerStatus.StopPending:
                        // possibly add kill process command
                        return errorHost;
                    case ServiceControllerStatus.StartPending:
                        return errorHost;
                    default:
                        return errorHost;
                }

                //return errorHost;

            }

        }

    }

    public static void Main(string[] args)
    {
        {
           // using (StreamWriter w = File.AppendText("C:\\Produmex\\Log\\ScaleCommLog.txt"))
           // {
           //     Log("ScaleComm Host: " +  " Showing negative weights. Restarted", w);
           // }
           // using (StreamReader r = File.OpenText("C:\\Produmex\\Log\\ScaleCommLog.txt"))
           // {
           //     DumpLog(r);
           // }
            try
            {
                string connectionString = ConnectDB();
                //ReadOrderData(connectionString);

                string PA = "172.16.35.6";      //kernelwrapper PA
                string SS = "172.16.35.168";    //aud75200gh SS
                string CS = "172.16.35.160";    //aud75200gy CS
                string NM = "172.16.35.150";     //aud75200j1 NM
                string SKP = "172.16.1.79";     //webpvts01 Server Kernel Pack
                string LWB = "172.16.35.82";    //leetweighbridge LWB
                string TWB = "172.16.6.71";      //wawgrif03 (Tabbita Weighbridge)

                //change to parsing ini/config file
                string[] hostArray = {
                SS,
                CS,
                NM,
                PA
            };

                // string[] hostArray = { "172.16.35.168", "172.16.35.160", "172.16.35.150", "172.16.35.6", "172.16.35.82", "172.16.6.71" };
                int hostCount = hostArray.Length;

                int port = 9991;    //ScaleComm Port

                int [] negCount = { 0, 0, 0, 0 };
                // Initialise loop
                while (true)
                {
                    int x = 0;
                    int y = 0;
                    
                    while (x < hostCount)
                    {
                        Console.WriteLine(hostArray[x]);
                        // Restart Scale Comm service if it fails
                        string serviceName = "PmxLogexScaleComm";
                        ServiceController sc = new ServiceController(serviceName, hostArray[x]);
                        int skip = 0;
                        //   int totalBytesReceived = sender.Receive(receiveBytes);
                        //   string txt = Encoding.ASCII.GetString(receiveBytes, 0, totalBytesReceived); //write variable to string
                        //   txt = txt.Replace(" ", "");

                        //string nameOfFile = "C:\\Produmex\\Log\\ScaleCommLog.txt"
                        string today = DateTime.Now.ToString("yyyyMMdd");
                        try
                        {
                            //Console.WriteLine(today);
                            FileInfo txtfile = new FileInfo("C:\\Produmex\\Log\\ScaleCommLog.txt");
                            //string newFileName = "C:\\Produmex\\Log\\ScaleCommLog" + today + ".txt";
                            if (txtfile.Length > (10 * 1024 * 1024))       // ## NOTE: 10MB max file size
                            {
                                System.IO.File.Move("C:\\Produmex\\Log\\ScaleCommLog.txt", "C:\\Produmex\\Log\\ScaleCommLog_" + today + ".txt");
                            }
                        }
                        catch { }

                        //Console.WriteLine(x);
                        //Console.WriteLine(hostArray[x]);

                        string txt = ConnectPort(hostArray[x], port);
                        txt = txt.Replace(" ", "");
                        x = x + 1;    //Increment counter

                        //Console.WriteLine(txt);   //Debugging Incoming string
                        //  string[] listA = txt.Split(" ");
                        string[] stringSeparators = new string[] { "msg=", ",", "name=", "cmd=", "net=", "uom=", "/>" };
                        string[] listA = txt.Split(stringSeparators, StringSplitOptions.None);


                        //foreach (string element in listA)
                        //    Console.WriteLine(element);

                        string ignore = listA[0];
                        string devName = listA[1];
                        // devName = Regex.Replace(devName, @"[^A-Z]+", ""); // regex replace
                        devName = devName.Replace("\"", "");

                        string cmd = listA[2];
                        cmd = cmd.Replace("\"", "");


                        //Test for Hello data
                        if (cmd.Contains("Hello"))
                        {
                            cmd = "skip";
                            skip = 1;
                            Console.WriteLine(cmd);
                            if (skip == 1)
                            {
                                //uom = "";
                            }
                        }

                        string netWeightTmp = listA[3];
                        int netWeight;
                        // Regex regex = new Regex(@"-?\d+");
                        //Match match = regex.Match(netWeightTmp);
                        //netWeightTmp = match.Value;
                        //int netWeight = Int32.Parse(netWeightTmp);
                        netWeightTmp = netWeightTmp.Replace("\"", "");

                        if (skip == 1)
                        {
                            netWeight = 0;
                        }
                        else
                        {
                            netWeight = Int32.Parse(netWeightTmp);
                            //netWeight = -5;

                            //Console.WriteLine(netWeightTmp);

                            Console.WriteLine(devName);
                            Console.WriteLine(cmd);
                            Console.WriteLine(netWeight);
                            using (StreamWriter w = File.AppendText("C:\\Produmex\\Log\\ScaleCommLog.txt"))
                            {
                                Log(", " + devName + ", " + cmd + ", " + netWeight, w);
                            }
                            

                            if (netWeight < 0)
                            {
                                
                                Console.WriteLine("< ZERO");
                                x = x - 1; // repeat check of value
                                
                                y = y + 1;
                                
                                Console.WriteLine(y);
                                Console.WriteLine("negcount: "+ negCount[x]);
                                Console.WriteLine("negcounts: " + negCount[0] + negCount[1] + negCount[2] + negCount[3] );
                                using (StreamWriter w = File.AppendText("C:\\Produmex\\Log\\ScaleCommLog.txt"))
                                {
                                    Log(", " + devName + ", " + "negcount: " + negCount[x] + ", "  + netWeight, w);
                                }

                                if (y == 10)
                                {
                                    negCount[x] = negCount[x] + 1;
                                    try
                                    {
                                        x = x + 1;
                                        //sc.Stop();
                                        //sc.Start();
                                        if (negCount[x] == 1 || negCount[x] == 200)
                                            try
                                            {
                                                //Console.WriteLine("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
                                                SendEmail("ScaleComm Host: " + hostArray[x] + " Showing negative weight. Restarted", "The scale continues to read a negative number after 10 checks. The scale comm service has now been restarted. Scale Weight: " + netWeight);
                                                using (StreamWriter w = File.AppendText("C:\\Produmex\\Log\\ScaleCommLog.txt"))
                                                {
                                                    Log("ScaleComm Host: " + hostArray[x] + " Showing negative weights. Restarted", w);
                                                }
                                                if (negCount[x] == 200)
                                                    try
                                                    {
                                                        negCount[x] = 0;
                                                    }
                                                    catch (Exception ex) { Console.WriteLine(ex.ToString()); }
                                                
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine(ex.ToString());
                                            }
                                        y = 0;
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex.ToString());
                                    }
                                }

                            }

                            Console.WriteLine();
                            // uom = listA[4];
                            // Console.WriteLine(uom);     //uom commented out as its not currently used by Produmex

                            WriteDB(connectionString, devName, cmd, netWeight);


                            //foreach (string element in listA)
                            //   Console.WriteLine(element);
                            //sender.Shutdown(SocketShutdown.Both);
                            //sender.Close();
                            //Console.ReadLine();
                        }
                    }
                    x = 0;
                }

            }
            
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    }
}

