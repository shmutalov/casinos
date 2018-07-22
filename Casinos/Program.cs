using System;
using System.Text.RegularExpressions;
using VkNet;
using VkNet.Model.RequestParams;
using MySql.Data.MySqlClient;
using Casinos;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.ComponentModel;
using VkNet.Model;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using VkNet.Enums.Filters;
using System.Linq;
using Microsoft.Extensions.Logging;
using NLog;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    static class Program
    {
        static ulong ts;
        static string key, server, req;

        static int money;
        static int peer_id;
        static long? userid;
        static long messageid;
        
        static World t = new World();
        static VkApi vk = new VkApi(null, new CptchCaptchaSolving());

        static void Win(int value, int number1, int number2)
        {
            int won = Convert.ToInt32((number1 * 100 * (100 * 1.0 / number2)));
            using (UserContext db = new UserContext())
            {
                var customer = db.users
                    .Where(c => c.userid == userid)
                    .FirstOrDefault();

                // Внести изменения
                customer.balance = money + won;
                db.users.Where(u => u.userid == userid).Take(1);
                db.SaveChanges();
            }
            Send(string.Format("Молодец, ты выиграл {0},{1}{2}. Число: {3} Твой баланс: {4},{5}{6}", won / 100, won % 100 / 10, won % 10, value, (money + won) / 100, (money + won) % 100 / 10, (money + won) % 10));
        }
        static void Loose(int value, int money)
        {
            Send(string.Format("Ты проиграл, пробуй ещё! Число: " + (value) + "\nБаланс: {0},{1}{2}", money / 100, money % 100 / 10, money % 10));
            using (UserContext db = new UserContext())
            {
                var customer = db.users
                    .Where(c => c.userid == userid)
                    .FirstOrDefault();

                // Внести изменения
                customer.balance = money;
                db.users.Where(u => u.userid == userid).Take(1);
                db.SaveChanges();
            }
        }

        static void Main(string[] args)
        {
            vk.Authorize(new ApiAuthParams { AccessToken = "token" });
            

            var longpoll = vk.Groups.GetLongPollServer(165855037);
            ts = longpoll.Ts;
            server = longpoll.Server;
            key = longpoll.Key;
            while (true)
            {
                try
                {
                    req = server + "?act=a_check&key=" + key + "&ts=" + ts + "&wait=25";
                    HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(req);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                    try { new StreamReader(response.GetResponseStream(), Encoding.UTF8); }
                    catch (Exception) { break; }

                    using (StreamReader stream = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        string Text = stream.ReadToEnd();
                        RootObject lp = JsonConvert.DeserializeObject<RootObject>(Text);
                        ts = ulong.Parse(lp.ts);

                        if (lp.updates[0].type == "message_new")
                        {
                            Task task = new Task(() =>
                            {
                                Logic(lp);
                            });
                            task.Start();

                            /*Thread t = new Thread(new ParameterizedThreadStart(Logic));
                            t.Start(lp);*/
                        }
                    }
                }
                catch (Exception e)
                {
                    File.AppendAllText(AppDomain.CurrentDomain.BaseDirectory + "log.txt", e.Message + e.StackTrace + Environment.NewLine);
                }
            }
        }
        static void Send(string message)
        {
            vk.Messages.Send(new VkNet.Model.RequestParams.MessagesSendParams
            {
                PeerId = peer_id,
                Message = message
            });
        }
        static void Bet(int value, int number1, int number2)
        {
            if (value <= number2) Win(value, number1, number2);
            else Loose(value, money);
        }
        static void Logic(RootObject lp)
        {
            userid = lp.updates[0].@object.from_id;
            peer_id = lp.updates[0].@object.peer_id;
            //messageid = Convert.ToInt64(lp.updates[0].@object.conversation_message_id);

            switch (lp.updates[0].@object.text)
            {
                case "/пополнить":
                    {
                        if (t.IfRegistered(userid)) Send("Введите /пополнить [сумма в рублях]");
                        else Send("Вы не зарегистрированы! Для регистрации введите /регистрация!");
                        break;
                    }
                case "/регистрация":
                    {
                        if (!t.IfRegistered(userid))
                        {
                            t.Register(userid);
                            Send("Вы успешно зарегистрировались.\n\nВам начислен бонус в сумме 10 рублей.\nДля игры введите /ставка [сумма] [вероятность]\nВводите только натуральные числа!\nИграйте без флуда, иначе бот не справится и вы можете потерять деньги(виноваты только вы сами)!");
                        }
                        else
                        {
                            int balanced = t.CheckBalance(userid);
                            Send(string.Format("Ваш аккаунт уже зарегистрирован. Ваш баланс: {0},{1}{2}", balanced / 100, balanced % 100 / 10, balanced % 10));
                        }
                        break;
                    }
                case "/ставка":
                    {
                        if (t.IfRegistered(userid)) Send("Введите /ставка [сумма] [вероятность]");
                        else Send("Ваш аккаунт не зарегистрирован. Для регистрации введите /регистрация.");
                        break;
                    }
                case "/баланс":
                    {
                        if (t.IfRegistered(userid))
                        {
                            int value = t.CheckBalance(userid);
                            Send(string.Format("Ваш баланс: {0},{1}{2}", value / 100, value % 100 / 10, value % 10));
                        }
                        else Send("Вы не зарегистрированы! Для регистрации введите /регистрация!");
                        break;
                    }
                case var s1 when (Regex.IsMatch(lp.updates[0].@object.text, @"/ставка (?'number1'\d+) (?'number2'\d+)")):
                    {
                        if (t.IfRegistered(userid))
                        {
                            var regex = new Regex(@"/ставка (?'number1'\d+) (?'number2'\d+)");
                            var match = regex.Match(lp.updates[0].@object.text);
                            int number1 = Convert.ToInt32(int.Parse(match.Groups["number1"].Value) * 1.00);
                            int number2 = Convert.ToInt32(int.Parse(match.Groups["number2"].Value) * 1.00);

                            money = t.CheckBalance(userid);
                            Random rand = new Random();
                            int value = rand.Next(0, 100);

                            if (number2 > 0 && 90 >= number2)
                            {
                                if (number1 <= 0) Send("Ставка не может быть меньше или равна нулю!");
                                else if (money / 100 < number1) Send("На балансе недостаточно денег!");
                                else
                                {
                                    money = (money - number1 * 100);
                                    //t.GameFirst(userid, money);

                                    Bet(value, number1, number2);
                                }
                            }
                            else Send("Вероятность выигрыша должна быть в пределах 0-90!");
                        }
                        else
                        {
                            Send("Вы не зарегистрированы! Для регистрации введите /регистрация!");
                        }
                        break;
                    }
            }
            /*var pattern_payment = @"/пополнить (?'number1'\d+)";
            var regex_payment = new Regex(pattern_payment);
            if (regex_payment.IsMatch(lp.updates[0].@object.text))
            {
                if (t.IfRegistered(userid))
                {
                    var match = regex_payment.Match(lp.updates[0].@object.text);
                    int number1 = Convert.ToInt32(int.Parse(match.Groups["number1"].Value));
                    Send(string.Format("Убедитесь в верности введённых данных: \nID аккаунта: {0}\nСумма пополнения: {1} рублей\n\nДля пополнения баланса на данный аккаунт перейдите по ссылке ниже:\nhttp://casinos-bot.com/payment/obr.php?account={0}&sum={1}&fk_go", userid, number1));
                }
                else Send("Ваш аккаунт не зарегистрирован. Для регистрации введите /регистрация.");
            }*/
        }
    }
    public class Object
    {
        public int from_id { get; set; }
        public string text { get; set; }
        public int peer_id { get; set; }
        public int conversation_message_id { get; set; }
    }

    public class Update
    {
        public string type { get; set; }
        public Object @object { get; set; }
        public int group_id { get; set; }
    }

    public class RootObject
    {
        public string ts { get; set; }
        public List<Update> updates { get; set; }
    }
    public class CptchCaptchaSolving : VkNet.Utils.AntiCaptcha.ICaptchaSolver
    {
        //Ключ нужно заменить на свой со страницы https://cptch.net/profile
        private const String CPTCH_API_KEY = "key";

        private const String CPTCH_UPLOAD_URL = "https://cptch.net/in.php";
        private const String CPTCH_RESULT_URL = "https://cptch.net/res.php";

        public string Solve(string url)
        {
            Console.WriteLine("Решаем капчу: " + url);
            //Скачиваем файл капчи из Вконтакте
            byte[] captcha = DownloadCaptchaFromVk(url);
            if (captcha != null)
            {
                //Загружаем файл на cptch.net
                string uploadResponse = UploadCaptchaToCptch(captcha);
                //Получаем из ответа id капчи
                string captchaId = ParseUploadResponse(uploadResponse);
                if (captchaId != null)
                {
                    Console.WriteLine("Id капчи: " + captchaId);
                    //Ждем несколько секунд
                    Thread.Sleep(1000);
                    //Делаем запрос на получение ответа до тех пор пока ответ не будет получен
                    string solution = null;
                    do
                    {
                        string solutionResponse = GetCaptchaSolution(getCaptchaRequestUri(captchaId));
                        solution = ParseSolutionResponse(solutionResponse);
                    } while (solution == null);

                    Console.WriteLine("Капча разгадана: " + solution);
                    return solution;
                }
            }
            else
            {
                Console.WriteLine("Не удалось скачать капчу с Вконтакте");
            }

            return null;
        }

        private string getCaptchaRequestUri(string captchaId)
        {
            return CPTCH_RESULT_URL + "?" + "key=" + CPTCH_API_KEY + "&action=get" + "&id=" + captchaId;
        }

        private byte[] DownloadCaptchaFromVk(string captchaUrl)
        {
            using (WebClient client = new WebClient())
            using (Stream s = client.OpenRead(captchaUrl))
            {
                return client.DownloadData(captchaUrl);
            }
        }

        private string UploadCaptchaToCptch(byte[] captcha)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                MultipartFormDataContent form = new MultipartFormDataContent();

                form.Add(new StringContent(CPTCH_API_KEY), "key");
                form.Add(new StringContent("post"), "method");
                form.Add(new ByteArrayContent(captcha, 0, captcha.Length), "file", "captcha");
                var response = httpClient.PostAsync(CPTCH_UPLOAD_URL, form).Result;
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = response.Content;
                    return responseContent.ReadAsStringAsync().Result;
                }
                else
                {
                    return null;
                }
            }
        }

        private string ParseUploadResponse(string uploadResponse)
        {
            if (uploadResponse.Contains("ERROR"))
            {
                Console.WriteLine("Возникла ошибка при загрузке капчи");
                return null;
            }
            else if (uploadResponse.Contains("OK"))
            {
                return uploadResponse.Split('|')[1];
            }
            return null;
        }

        public static String GetCaptchaSolution(string captchaSolutionUrl)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(captchaSolutionUrl);

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private string ParseSolutionResponse(string response)
        {
            if (response.Equals("ERROR"))
            {
                Console.WriteLine("Ошибка во время получения ответа: " + response);
                return null;
            }
            else if (response.Equals("CAPCHA_NOT_READY"))
            {
                Console.WriteLine("Капча еще не готова");
                Thread.Sleep(1000);
                return null;
            }
            else if (response.Contains("OK"))
            {
                return response.Split('|')[1];
            }
            return null;
        }

        public void CaptchaIsFalse()
        {
            Console.WriteLine("Последняя капча была распознана неверно");
        }
    }
}
