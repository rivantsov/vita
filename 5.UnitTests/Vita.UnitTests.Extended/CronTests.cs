using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vita.Modules.Calendar.Cron;

namespace Vita.UnitTests.Extended {

  [TestClass]
  public class CronTests {

    [TestMethod]
    public void TestCronScheduler() {
      DateTime nextDt;

      //Day of month tests
      //23:05 25th of every month, years 2016, 2020
      var cronMonthly = "05 23 25 * * 2016,2020";
      TestCronNext(cronMonthly, "2016-10-13", "2016-10-25 23:05");
      // find next, now should be Nov 25
      TestCronNext(cronMonthly, "2016-10-26", "2016-11-25 23:05");
      //next - Dec 25
      TestCronNext(cronMonthly, "2016-11-25 23:05", "2016-12-25 23:05");
      // next - Jan 25, 2020
      TestCronNext(cronMonthly, "2016-12-25 23:05", "2020-01-25 23:05");
      // allow 5 segments (without year); star in minutes is replaced with 0
      cronMonthly = "* 23 25 * *";
      TestCronNext(cronMonthly, "2016-10-13", "2016-10-25 23:00");

      // Day 31 should be treated as 'last day' of any month
      var cron31 = "0 0 31 * * *";
      TestCronNext(cron31, "2016-11-10", "2016-11-30"); //Nov has 30 days, but we hit last day 30

      // Day of week
      var cronWk = "15 12 * * Wed,Friday *"; //12:15 every Wed, Fri; using short and long names
      TestCronNext(cronWk, "2016-10-15", "2016-10-19 12:15:00"); //Sat Oct 14 -> Wed Oct 19
      //next should be Fri
      TestCronNext(cronWk, "2016-10-19 15:00", "2016-10-21 12:15"); // -> Fri Oct 21
      // 0 in day of week is Sunday, but 7 is Sunday as well
      cronWk = "15 12 * * 7 *"; //12:15 every Sunday; 
      TestCronNext(cronWk, "2016-10-12", "2016-10-16 12:15"); // -> Sun Oct 16

      // Day of week with # specifier: Fri#3 -> third Fri of the month
      cronWk = "0 0 * * Fri#3 *";
      nextDt = TestCronNext(cronWk, "2016-10-01", "2016-10-21"); // Oct 21, Fri
      nextDt = TestCronNext(cronWk, nextDt, "2016-11-18"); //Nov 18, Fri
      // Special case: Fri#5 matches LAST Fri (#4) if there are only 4 Fridays in the month
      cronWk = "0 0 * * Fri#5 *";
      TestCronNext(cronWk, "2016-10-01", "2016-10-28"); // Oct 28, Fri #4

      // Using '/', ex: '/5' - days dividing by 5 
      // Schedule: midnight on 1st every 6 months, in years divisible by 20; notice using two variants: '*/6' and '/20'
      TestCronNext("0 0 1 */6 * /20", "2016-10-12" /*Wed*/, "2020-06-01");
      //Every 2 hours
      nextDt = TestCronNext("0 /2 * * * *", "2016-10-12", "2016-10-12 02:00");
      nextDt = TestCronNext("0 /2 * * * *", nextDt, "2016-10-12 04:00");
      nextDt = TestCronNext("0 /2 * * * *", nextDt, "2016-10-12 06:00");

      // Using both Day and DayOfWeek
      // Run on Fri, 13 only at 13:13
      TestCronNext("13 13 13 * 5 *", "2016-10-14", "2017-01-13 13:13");
      // next is Fri, Oct 13 2017
      TestCronNext("13 13 13 * 5 *", "2017-01-14", "2017-10-13 13:13");
      // Run on Feb 29
      TestCronNext("0 0 29 2 * *", "2016-10-14", "2020-02-29");

      // W - weekday specifier
      TestCronNext("0 0 15W * * *", "2016-10-10", "2016-10-14"); //15 Oct is Sat, should shift to Fri 14
      TestCronNext("0 0 16W * * *", "2016-10-10", "2016-10-17"); //16 Oct is Sun, should shift to Mon 17
      // W - shift should not cross month boundary; 
      //  July 31, 2016 is Sun, so should shift to Fri, not Mon which is closer, but is in August
      TestCronNext("0 0 31W * * *", "2016-07-10", "2016-07-29"); // shifts to Fri 29
      // With W> shift is always forward; same condition, now shifts forward to Mon accross month boundaries
      TestCronNext("0 0 31W> * * *", "2016-07-10", "2016-08-01"); // shifts to Mon Aug 1
      // W< - shift to prev; Jan 1 2017 is Sun; we force shift to prev
      TestCronNext("0 0 1W< 1 * *", "2016-12-10", "2016-12-30"); // shifts to Fri Dec 30

    }

    private DateTime TestCronNext(string cron, string date, string expectedDate) {
      var dt = DateTime.Parse(date);
      return TestCronNext(cron, dt, expectedDate);
    }
    private DateTime TestCronNext(string cron, DateTime date, string expectedDate) {
      var sched = new CronSchedule(cron);
      var next = sched.TryGetNext(date);
      Assert.IsNotNull(next, "Expected next, CRON: '" + cron + "', date: " + date);
      var expected = DateTime.Parse(expectedDate);
      Assert.AreEqual(expected, next.Value, "Next date does not match expected.");
      return next.Value;
    }


  }//class
}//ns 
