using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using System.Net;
using AntiCaptchaAPI;
using System.Text;

class Program
{
    private static IWebDriver driver;

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        Console.WriteLine("Використані методи для обходу reCAPTCHA:");
        Console.WriteLine("- Імітація поведінки людини через Actions (рухи миші, паузи)");
        Console.WriteLine("- Автоматичне розв'язання капчі через AntiCaptcha API (якщо є ключ)");
        Console.WriteLine("- Можливість ручного введення рішення, якщо автоматичний метод не спрацював");
        Console.WriteLine("Використані налаштування ChromeOptions для обходу reCAPTCHA:");
        Console.WriteLine("- Вимкнення виявлення автоматизації (--disable-blink-features=AutomationControlled)");
        Console.WriteLine("- Встановлення кастомного User-Agent для імітації реального користувача");
        Console.WriteLine();

        Console.CancelKeyPress += OnExit;
        AppDomain.CurrentDomain.ProcessExit += OnExit;

        ChromeDriverService chromeService = null;

        try
        {
            File.WriteAllText("..\\..\\..\\out.log", string.Empty);
            chromeService = ChromeDriverService.CreateDefaultService();
            chromeService.SuppressInitialDiagnosticInformation = false;
            chromeService.HideCommandPromptWindow = true;
            chromeService.LogPath = "..\\..\\..\\out.log";

            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/98.0.4758.102 Safari/537.36");

            driver = new ChromeDriver(chromeService, options);
            driver.Navigate().GoToUrl("https://www.linkedin.com/login");

            Console.Write("Введіть вашу електронну пошту: ");
            string email = Console.ReadLine();
            Console.Write("Введіть ваш пароль: ");
            string password = Console.ReadLine();

            LinkedIn linkedIn = new LinkedIn(email, password, driver);
            linkedIn.SimulateHumanInteraction();
            linkedIn.LoginToLinkedIn();
            Thread.Sleep(5000);
            linkedIn.GetSrcImageAndDownload();
        }
        finally
        {
            driver?.Quit();
            chromeService?.Dispose();
        }

    }
    private static void OnExit(object sender, EventArgs e)
    {
        Console.WriteLine("Завершення роботи... Закриття драйвера.");
        driver?.Quit();
    }
}

class LinkedIn
{
    private readonly string Email;
    private readonly string Password;
    private readonly IWebDriver Driver;

    public LinkedIn(string email, string password, IWebDriver driver)
    {
        Email = email;
        Password = password;
        Driver = driver;
    }

    public bool LoginToLinkedIn()
    {
        try
        {
            Driver.FindElement(By.Id("username")).SendKeys(Email);
            Driver.FindElement(By.Id("password")).SendKeys(Password);

            var recaptchaHandler = new RecaptchaHandler(Driver);
            if (recaptchaHandler.IsRecaptchaPresent())
            {
                recaptchaHandler.SolveRecaptchaAsync().Wait();
            }

            Driver.FindElement(By.XPath("//div[contains(@class, 'login__form_action_container')]//button[@type='submit']")).Click();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    public bool GetSrcImageAndDownload()
    {
        try
        {
            string imageSrc = Driver.FindElement(By.XPath("//a[contains(@class, 'profile-card-profile-picture-container')]/img"))
                                    .GetAttribute("src");
            string imageName = Driver.FindElement(By.XPath("//a[contains(@class, 'profile-card-profile-picture-container')]/img"))
                                      .GetAttribute("alt");
            return DownloadImage(imageSrc, imageName);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    private bool DownloadImage(string imageSrc, string imageName)
    {
        try
        {
            string filePath = Path.Combine("..\\..\\..\\img\\", imageName + ".jpg");
            if (File.Exists(filePath))
            {
                Console.WriteLine("Зображення вже існує");
                return false;
            }

            using WebClient downloader = new WebClient();
            downloader.DownloadFile(imageSrc, filePath);
            Console.WriteLine("Зображення успішно завантажено");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    public void SimulateHumanInteraction()
    {
        try
        {
            Actions actions = new Actions(Driver);
            actions.MoveByOffset(100, 100).Pause(TimeSpan.FromSeconds(1))
                   .MoveByOffset(50, 50).Pause(TimeSpan.FromSeconds(1))
                   .MoveByOffset(-30, 20).Pause(TimeSpan.FromSeconds(1))
                   .Perform();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}

class RecaptchaHandler
{
    private readonly IWebDriver Driver;

    public RecaptchaHandler(IWebDriver driver)
    {
        Driver = driver;
    }

    public bool IsRecaptchaPresent()
    {
        try
        {
            return Driver.FindElements(By.XPath("//iframe[contains(@src, 'recaptcha')]")).Count > 0 ||
                   Driver.FindElements(By.ClassName("g-recaptcha")).Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task SolveRecaptchaAsync()
    {
        try
        {
            Console.Write("У вас є ключ AntiCaptcha API? (так/ні): ");
            if (Console.ReadLine()?.ToLower() != "так") throw new Exception("API-ключ відхилено");

            Console.Write("Введіть ваш AntiCaptcha API ключ: ");
            var apiKey = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(apiKey)) throw new Exception("Невірний API-ключ");

            var siteKey = Driver.FindElement(By.CssSelector(".g-recaptcha")).GetAttribute("data-sitekey");
            var pageUrl = Driver.Url;

            var captcha = new AntiCaptcha(apiKey);
            var reCaptchaSolution = await captcha.SolveReCaptchaV2(siteKey, pageUrl);

            if (reCaptchaSolution.Success)
            {
                ((IJavaScriptExecutor)Driver).ExecuteScript(
                    $"document.getElementById('g-recaptcha-response').innerHTML='{reCaptchaSolution}';"
                );
            }
            else
            {
                Console.WriteLine("Не вдалося вирішити reCAPTCHA");
            }
        }
        catch
        {
            ManualCaptchaSolve();
        }
    }

    private void ManualCaptchaSolve()
    {
        Console.WriteLine("Будь ласка, вирішіть CAPTCHA вручну і натисніть Enter...");
        Console.ReadLine();
    }
}