using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.Extensions;
using OpenQA.Selenium.Support.UI;

namespace GeeTestVertifyCode
{
    public interface ISlideVerificationCode
    {
        bool Pass(RemoteWebDriver remoteWebDriver);
    }

    public class SeleniumVertifyCode : ISlideVerificationCode
    {
        #region 属性

        /// <summary>
        /// 拖动按钮
        /// </summary>
        private const string SlidButton = "gt_slider_knob";

        /// <summary>
        /// 原始图层
        /// </summary>
        private const string OriginalMap = "gt_fullbg";

        /// <summary>
        /// 原始图加缺口背景图
        /// </summary>
        private const string NewMap = "gt_bg";

        /// <summary>
        /// 缺口图层
        /// </summary>
        private const string SliceMap = "gt_slice";

        private const int WaitTime = 100;

        /// <summary>
        /// 重试次数
        /// </summary>
        private const int TryTimes = 6;

        /// <summary>
        /// 缺口图默认偏移像素
        /// </summary>
        private const int LeftOffset = 4;

        private const string FullScreenPath = "全屏.png";
        private const string OriginalMapPath = "原图.png";
        private const string NewMapPath = "新图.png";

        #endregion 属性

        public void StartGeeTest(Uri uri)
        {
            const int waitTime = 500;
            var options = new OpenQA.Selenium.Chrome.ChromeOptions();
            //options.AddArgument("-headless");//不显示界面
            options.AddArgument("--window-size=1920,1050");
            options.AddArgument("log-level=3");
            using OpenQA.Selenium.Chrome.ChromeDriver driver = new OpenQA.Selenium.Chrome.ChromeDriver(options);
            driver.Navigate().GoToUrl(uri);
            //反爬验证webdriver
            driver.ExecuteJavaScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");
            driver.Navigate().GoToUrl(uri);
            Thread.Sleep(waitTime);//等待js执行
            driver.ExecuteScript("header.loginLink(event)");
            Console.WriteLine("点击《登录/注册》按钮");
            Thread.Sleep(waitTime);
            driver.ExecuteScript("loginObj.changeCurrent(1);");
            Console.WriteLine("点击 《密码登录》按钮");
            Thread.Sleep(waitTime);
            driver.ExecuteScript("$('.contactphone').val('17712345678')");
            driver.ExecuteScript("$('.contactword').val('2020')");
            Console.WriteLine("输入账号密码");
            Thread.Sleep(waitTime);
            driver.ExecuteScript("loginObj.loginByPhone(event);");
            Console.WriteLine("点击《登录》按钮");
            Thread.Sleep(waitTime);
            SeleniumVertifyCode slideVerificationCode = new SeleniumVertifyCode();
            var flag = slideVerificationCode.Pass(driver);
            Console.WriteLine($"验证结果{flag}");
        }

        public bool Pass(RemoteWebDriver remoteWebDriver)
        {
            const int waitTime = 6000;
            int failTimes = 0;
            bool flag = false;
            do
            {
                ScreenMap(remoteWebDriver);
                var distance = GetDistance();
                if (distance > 0)
                {
                    Console.WriteLine($"开始获取移动轨迹...");
                    var moveEntitys = GetMoveEntities(distance);
                    Move(remoteWebDriver, moveEntitys);
                    Console.WriteLine("休眠3秒,显示等待提交验证码...");
                    Thread.Sleep(waitTime);
                    Console.WriteLine("开始检查认证是否通过...");
                    flag = CheckSuccess(remoteWebDriver);
                    if (flag)
                    {
                        break;
                    }
                }
            } while (++failTimes < TryTimes);
            return flag;
        }

        public void GetCookie(RemoteWebDriver remoteWebDriver)
        {
            List<string> list = new List<string>();
            foreach (var item in remoteWebDriver.Manage().Cookies.AllCookies)
            {
                list.Add($"{item.Name}={item.Value}");
            }
            System.IO.File.WriteAllLines("Cookie.txt", list);
        }

        protected virtual bool CheckSuccess(RemoteWebDriver remoteWebDriver)
        {
            const int waitTime = 6000;
            try
            {
                remoteWebDriver.FindElement(By.ClassName(SlidButton));
                Console.WriteLine("验证失败,显示等待6秒刷新验证码...");
                Thread.Sleep(waitTime);
                return false;
            }
            catch (NoSuchElementException)
            {
                GetCookie(remoteWebDriver);
                return true;
            }
        }

        private void Move(RemoteWebDriver remoteWebDriver, List<MoveEntity> moveEntities)
        {
            var slidButton = GetSlidButtonElement(remoteWebDriver);
            Actions builder = new Actions(remoteWebDriver);
            builder.ClickAndHold(slidButton).Perform();
            int offset = 0;
            int index = 0;
            foreach (var item in moveEntities)
            {
                index++;
                builder = new Actions(remoteWebDriver);
                builder.MoveByOffset(item.X, item.Y).Perform();
                Console.WriteLine("向右总共移动了:" + (offset = offset + item.X));
            }
            builder.Release().Perform();
        }

        private List<MoveEntity> GetMoveEntities(int distance)
        {
            List<MoveEntity> moveEntities = new List<MoveEntity>();
            int allOffset = 0;
            do
            {
                int offset = 0;
                double offsetPercentage = allOffset / (double)distance;

                if (offsetPercentage > 0.5)
                {
                    if (offsetPercentage < 0.85)
                    {
                        offset = new Random().Next(10, 20);
                    }
                    else
                    {
                        offset = new Random().Next(2, 5);
                    }
                }
                else
                {
                    offset = new Random().Next(20, 30);
                }
                allOffset += offset;
                int y = (new Random().Next(0, 1) == 1 ? new Random().Next(0, 2) : 0 - new Random().Next(0, 2));
                moveEntities.Add(new MoveEntity(offset, y, offset));
            } while (allOffset <= distance + 5);
            var moveOver = allOffset > distance;
            for (int j = 0; j < Math.Abs(distance - allOffset);)
            {
                int step = 3;

                int offset = moveOver ? -step : step;
                int sleep = new Random().Next(100, 200);
                moveEntities.Add(new MoveEntity(offset, 0, sleep)); ;

                j = j + step;
            }
            return moveEntities;
        }

        /// <summary>
        /// 比较两张图片的像素，确定阴影图片位置
        /// </summary>
        /// <param name="oldBmp"></param>
        /// <param name="newBmp"></param>
        /// <returns></returns>
        private int GetArgb(Bitmap oldBmp, Bitmap newBmp)
        {
            //由于阴影图片四个角存在黑点(矩形1*1)
            for (int i = 0; i < newBmp.Width; i++)
            {
                for (int j = 0; j < newBmp.Height; j++)
                {
                    if ((i >= 0 && i <= 1) && ((j >= 0 && j <= 1) || (j >= (newBmp.Height - 2) && j <= (newBmp.Height - 1))))
                    {
                        continue;
                    }
                    if ((i >= (newBmp.Width - 2) && i <= (newBmp.Width - 1)) && ((j >= 0 && j <= 1)
                        || (j >= (newBmp.Height - 2) && j <= (newBmp.Height - 1))))
                    {
                        continue;
                    }

                    //获取该点的像素的RGB的颜色
                    Color oldColor = oldBmp.GetPixel(i, j);
                    Color newColor = newBmp.GetPixel(i, j);
                    if (Math.Abs(oldColor.R - newColor.R) > 60 || Math.Abs(oldColor.G - newColor.G) > 60
                        || Math.Abs(oldColor.B - newColor.B) > 60)
                    {
                        return i;
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// 获取实际图层缺口实际距离
        /// </summary>
        /// <returns></returns>
        private int GetDistance()
        {
            using Bitmap oldBitmap = (Bitmap)Image.FromFile(OriginalMapPath);
            using Bitmap newBitmap = (Bitmap)Image.FromFile(NewMapPath);
            var distance = GetArgb(oldBitmap, newBitmap);
            distance -= LeftOffset;
            Console.WriteLine($"缺口距离{distance}");
            return distance;
        }

        /// <summary>
        /// 截图
        /// </summary>
        /// <param name="remoteWebDriver"></param>
        private void ScreenMap(RemoteWebDriver remoteWebDriver)
        {
            //显示原始图
            ShowOriginalMap(remoteWebDriver);
            //全屏截图
            FullScreen(remoteWebDriver);
            //获取原始图层
            var originalElement = GetOriginalElement(remoteWebDriver);
            //保存原始图
            CutBitmap(FullScreenPath, OriginalMapPath, originalElement);

            //显示新图层
            ShowNewMap(remoteWebDriver);
            //全屏截图
            FullScreen(remoteWebDriver);
            //获取新图层
            var newElement = GetNewMapElement(remoteWebDriver);
            //保存新图
            CutBitmap(FullScreenPath, NewMapPath, newElement);
            //显示缺口图
            ShowSliceMap(remoteWebDriver);
        }

        /// <summary>
        /// 截图
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="targetPath"></param>
        /// <param name="webElement"></param>
        private void CutBitmap(string sourcePath, string targetPath, IWebElement webElement)
        {
            using var bitmap = (Bitmap)Image.FromFile(sourcePath);
            using var newBitmap = bitmap.Clone(new Rectangle(webElement.Location, webElement.Size),
                System.Drawing.Imaging.PixelFormat.DontCare);
            newBitmap.Save(targetPath);
        }

        /// <summary>
        /// 全屏截图
        /// </summary>
        /// <param name="remoteWebDriver"></param>
        private void FullScreen(RemoteWebDriver remoteWebDriver)
        {
            remoteWebDriver.GetScreenshot().SaveAsFile(FullScreenPath);
        }

        /// <summary>
        /// 获取原始图层元素
        /// </summary>
        /// <param name="remoteWebDriver"></param>
        /// <returns></returns>
        protected virtual IWebElement GetOriginalElement(RemoteWebDriver remoteWebDriver)
        {
            return remoteWebDriver.FindElementExtension(By.ClassName(OriginalMap));
        }

        /// <summary>
        /// 获取原始图加缺口背景图元素
        /// </summary>
        /// <param name="remoteWebDriver"></param>
        /// <returns></returns>
        protected virtual IWebElement GetNewMapElement(RemoteWebDriver remoteWebDriver)
        {
            return remoteWebDriver.FindElementExtension(By.ClassName(NewMap));
        }

        /// <summary>
        /// 获取缺口图层元素
        /// </summary>
        /// <param name="remoteWebDriver"></param>
        /// <returns></returns>
        protected virtual IWebElement GetSliceMapElement(RemoteWebDriver remoteWebDriver)
        {
            return remoteWebDriver.FindElementExtension(By.ClassName(SliceMap));
        }

        /// <summary>
        /// 获取拖动按钮元素
        /// </summary>
        /// <param name="remoteWebDriver"></param>
        /// <returns></returns>
        protected virtual IWebElement GetSlidButtonElement(RemoteWebDriver remoteWebDriver)
        {
            return remoteWebDriver.FindElementExtension(By.ClassName(SlidButton));
        }

        /// <summary>
        /// 显示原始图层
        /// </summary>
        /// <param name="remoteWebDriver"></param>
        protected virtual bool ShowOriginalMap(RemoteWebDriver remoteWebDriver)
        {
            remoteWebDriver.ExecuteScript
                ("$('." + NewMap + "').hide();$('." + OriginalMap + "').show();$('." + SliceMap + "').hide();");
            Console.WriteLine("显示原始图");
            Thread.Sleep(WaitTime);
            return true;
        }

        /// <summary>
        /// 显示原始图加缺口背景之后的图层
        /// </summary>
        /// <param name="remoteWebDriver"></param>
        /// <returns></returns>
        protected virtual bool ShowNewMap(RemoteWebDriver remoteWebDriver)
        {
            remoteWebDriver.ExecuteScript
                ("$('." + NewMap + "').show();$('." + OriginalMap + "').hide();$('." + SliceMap + "').hide();");
            Console.WriteLine("显示原始图加缺口背景之后的图层");
            Thread.Sleep(WaitTime);
            return true;
        }

        /// <summary>
        /// 显示缺口图
        /// </summary>
        /// <param name="remoteWebDriver"></param>
        /// <returns></returns>
        protected virtual bool ShowSliceMap(RemoteWebDriver remoteWebDriver)
        {
            remoteWebDriver.ExecuteScript("$('." + SliceMap + "').show();");
            Console.WriteLine("显示原始图加缺口背景之后的图层");
            Thread.Sleep(WaitTime);
            return true;
        }
    }

    internal class MoveEntity
    {
        public int X;
        public int Y;
        public int sleep;

        public MoveEntity(int offset, int v, int sleep)
        {
            this.X = offset;
            this.Y = v;
            this.sleep = sleep;
        }
    }

    public static class WebElementExtensions
    {
        public static IWebElement FindElementExtension(this IWebDriver driver, By by, int timeoutInSeconds = 10)
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
            return wait.Until(d => driver.FindElement(by));
        }
    }
}