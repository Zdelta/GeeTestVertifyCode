using System;

namespace GeeTestVertifyCode
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            new SeleniumVertifyCode().StartGeeTest(new Uri("https://www.tianyancha.com/"));
        }
    }
}