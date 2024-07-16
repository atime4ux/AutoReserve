using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Threading;

namespace AutoReserve
{
    public static class WebDriverExtension
    {
        public static bool IsElementVisible(this IWebDriver driver, By by)
        {
            bool result = false;

            try
            {
                var elem = driver.FindElement(by);
                result = elem.Displayed;
            }
            catch (Exception ex)
            { }

            return result;
        }

        public static bool IsElementClickable(this IWebDriver driver, By by)
        {
            bool result = false;

            try
            {
                var elem = driver.FindElement(by);
                result = elem.Displayed && elem.Enabled;
            }
            catch (Exception ex)
            { }

            return result;
        }

        public static bool ProcessKakaoPay(this IWebDriver driver, IWebElement kakaoPayArea, string checkoutPhone, string checkoutBirth)
        {
            bool result = false;

            driver.SwitchTo().Frame(kakaoPayArea);

            WebDriverWait kakaoPayWait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            //kakaoPayWait.PollingInterval = TimeSpan.FromMilliseconds(200);

            try
            {
                //try
                //{
                //    //계속하기 버튼 있으면 클릭...
                //    kakaoPayWait.Until(x => x.IsElementClickable(By.CssSelector(".btn_pay")));
                //    driver.FindElement(By.CssSelector(".btn_pay")).Click();
                //}
                //catch (Exception ex)
                //{ }

                kakaoPayWait.Until(x => x.IsElementClickable(By.CssSelector(".button-menu.kakaotalk")));
                driver.FindElement(By.CssSelector(".button-menu.kakaotalk")).Click();

                kakaoPayWait.Until(x => x.IsElementClickable(By.CssSelector("#userPhone")));
                driver.FindElement(By.CssSelector("#userPhone")).SendKeys(checkoutPhone);
                driver.FindElement(By.CssSelector("#userBirth")).SendKeys(checkoutBirth);

                kakaoPayWait.Until(x => x.IsElementClickable(By.CssSelector(".btn_payask")));
                driver.FindElement(By.CssSelector(".btn_payask")).Click();

                kakaoPayWait.Until(x => x.IsElementClickable(By.CssSelector(".button-finish.btn_submit")));
                string prevCheckoutUrl = driver.Url;

                DateTime checkoutTime = DateTime.Now;
                while (prevCheckoutUrl == driver.Url)
                {
                    if ((DateTime.Now - checkoutTime).TotalSeconds > 10)
                    {
                        //2분동안 결제 안되면 취소
                        break;
                    }

                    driver.FindElement(By.CssSelector(".button-finish.btn_submit")).Click();
                    while (prevCheckoutUrl == driver.Url)
                    {
                        try
                        {
                            var alertPopup = driver.FindElement(By.CssSelector("#payAlertDiv"));
                            if (alertPopup.GetAttribute("class").Contains("hide"))
                            {
                                Thread.Sleep(250);
                            }
                            else
                            {
                                driver.FindElement(By.CssSelector("#alertOkButton")).Click();
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            //결제 완료 후 화면 이동
                            result = true;
                            break;
                        }
                    }

                    if (result)
                    {
                        break;
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            { }

            driver.SwitchTo().DefaultContent();

            return result;
        }
    }
}
