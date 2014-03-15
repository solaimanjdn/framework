using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Signum.Utilities;
using System.Globalization;
using System.Web.Script.Serialization;
using System.Web;
using System.Linq.Expressions;
using Signum.Web.Lines;
using System.Web.Mvc.Html;
using Signum.Entities;
using Signum.Entities.DynamicQuery;
using System.Text.RegularExpressions;
using Signum.Utilities.DataStructures;

namespace Signum.Web
{
    public static class CalendarHelper
    {
        static readonly Regex parts = new Regex(@"(\w)\1{0,}");

        public static DateTimeFormat SplitDateTimeFormat(string dateTimeFormat, CultureInfo culture = null)
        {
            if (culture == null)
                culture = CultureInfo.CurrentCulture;

            string customFormat = Customize(dateTimeFormat, culture.DateTimeFormat);

            var matches = parts.Matches(customFormat).Cast<Match>();

            if (matches.All(m => IsDate(m.Value)))
                return new DateTimeFormat { DateFormat = customFormat, TimeFormat = null };

            var max = matches.Where(m => IsDate(m.Value)).Max(a => a.Index + a.Length);

            if (matches.Any(m => !IsDate(m.Value) && m.Index < max))
                throw new FormatException("Impossible to split {0} in Date and Time".Formato(dateTimeFormat));

            var min = matches.Where(m => !IsDate(m.Value)).Min(a => a.Index);

            return new DateTimeFormat { DateFormat = customFormat.Substring(0, max), TimeFormat = customFormat.Substring(min) };
        }

        public struct DateTimeFormat
        {
            public string DateFormat;
            public string TimeFormat; 
        }

        private static string ToJsDateFormat(string dateFormat)
        {
            return parts.Replace(dateFormat, m =>
            {
                switch (m.Value)
                {
                    case "d": return "d";
                    case "dd": return "dd";
                    case "ddd": return "D";
                    case "dddd": return "DD";
                    case "M": return "m";
                    case "MM": return "mm";
                    case "MMM": return "M";
                    case "MMMM": return "MM";
                    case "yy": return "yy";
                    case "yyyy": return "yyyy";
                    default: throw new InvalidOperationException("Unexpected part " + parts);
                }
            }); 
        }

        private static bool IsDate(string part)
        {
            switch (part)
            {
                case "d": 
                case "dd": 
                case "ddd": 
                case "dddd": 
                case "M":
                case "MM": 
                case "MMM": 
                case "MMMM": 
                case "yy":
                case "yyyy": return true;
                default: return false;
            }
        }

        private static string Customize(string format, DateTimeFormatInfo info)
        {
            switch (format)
            {
                case "d": return info.ShortDatePattern;
                case "D": return info.LongDatePattern;
                case "f": return info.LongDatePattern + " " + info.ShortTimePattern;
                case "F": return info.FullDateTimePattern;
                case "g": return info.ShortDatePattern + " " + info.ShortTimePattern;
                case "G": return info.ShortDatePattern + " " + info.LongTimePattern;
                case "m":
                case "M": return info.MonthDayPattern;
                case "r":
                case "R": return info.RFC1123Pattern;
                case "s": return info.SortableDateTimePattern;
                case "t": return info.ShortTimePattern;
                case "T": return info.LongTimePattern;
                case "u": return info.UniversalSortableDateTimePattern;
                case "U": return info.FullDateTimePattern;
                case "y":
                case "Y": return info.YearMonthPattern;
                default: return format;
            }
        }


        public static MvcHtmlString DateTimePicker(this HtmlHelper helper, string name, bool formGroup, DateTime? value, string dateTimeFormat, CultureInfo culture = null, IDictionary<string, object> htmlProps = null)
        {
            var dateFormat = SplitDateTimeFormat(dateTimeFormat, culture);

            HtmlStringBuilder sb = new HtmlStringBuilder();
            if(dateFormat.DateFormat != null)
                sb.Add(helper.DatePicker(dateFormat.TimeFormat == null? name : TypeContextUtilities.Compose(name, "Date"), formGroup, value == null ? "" : value.Value.ToString(dateFormat.DateFormat, culture), ToJsDateFormat(dateFormat.DateFormat), culture, htmlProps));

            if (dateFormat.TimeFormat != null)
            {
                if (dateFormat.TimeFormat.Contains("f") || dateFormat.TimeFormat.Contains("F"))
                {
                    htmlProps["class"] += " form-control";
                    sb.Add(helper.TextBox(TypeContextUtilities.Compose(name, "Time"), value == null ? "" : value.Value.ToString(dateFormat.TimeFormat, culture), htmlProps));
                }
                else
                    sb.Add(helper.TimePicker(TypeContextUtilities.Compose(name, "Time"), formGroup, value == null ? "" : value.Value.ToString(dateFormat.TimeFormat, culture), dateFormat.TimeFormat, htmlProps));
            }

            return sb.ToHtml();
        }

        public static MvcHtmlString DatePicker(this HtmlHelper helper, string name, bool formGroup, DateTime? value, string dateTimeFormat, CultureInfo culture = null, IDictionary<string, object> htmlProps = null)
        {
            var dateFormat = SplitDateTimeFormat(dateTimeFormat, culture);

            return helper.DatePicker(name, formGroup, value == null ? "" : value.Value.ToString(dateFormat.DateFormat, culture), ToJsDateFormat(dateFormat.DateFormat), culture);
        }

        public static MvcHtmlString DatePicker(this HtmlHelper helper, string name, bool formGroup, string value, string jsFormat, CultureInfo culture = null, IDictionary<string, object> htmlProps = null)
        {
            if (culture == null)
                culture = CultureInfo.CurrentCulture;

            var input = new HtmlTag("input")
                .IdName(name)
                .Attr("type", "text")
                .Class("form-control")
               
                .Attrs(htmlProps)
                .Attr("value", value);
               
            if(!formGroup)
                return AttachDatePicker(input, culture, jsFormat);
            
            HtmlStringBuilder sb = new HtmlStringBuilder();
            using (sb.Surround(AttachDatePicker(new HtmlTag("div").Class("input-group date"), culture, jsFormat)))
            {
                sb.Add(input);

                using (sb.Surround(new HtmlTag("span").Class("input-group-addon")))
                    sb.Add(new HtmlTag("span").Class("glyphicon glyphicon-calendar"));
            }

            return sb.ToHtml();
        }

        private static HtmlTag AttachDatePicker(HtmlTag tag, CultureInfo culture, string jsFormat)
        {
            return tag.Attr("data-provide", "datepicker")
               .Attr("data-language", culture.TwoLetterISOLanguageName)
               .Attr("data-date-autoclose", "true")
               .Attr("data-date-format", jsFormat)
               .Attr("data-date-today-btn", "linked")
               .Attr("data-date-today-highlight", "true");
        }

        public static MvcHtmlString TimePicker(this HtmlHelper helper, string name, bool formGroup, TimeSpan? ts, string format, CultureInfo culture = null, IDictionary<string, object> htmlProps = null)
        {
            if (format.Contains("f") || format.Contains("F"))
                return helper.TextBox(name, ts == null ? "" : ts.Value.ToString(format, culture), new { @class = "form-control" });

            return helper.TimePicker(name, formGroup, ts == null ? "" : ts.Value.ToString(format, culture), format, htmlProps: htmlProps);
        }

        public static MvcHtmlString TimePicker(this HtmlHelper helper, string name, bool formGroup, string value, string format, IDictionary<string, object> htmlProps = null)
        {
            var input = new HtmlTag("input")
                .IdName(name)
                .Attr("type", "text")
                .Class("form-control")
                .Attrs(htmlProps)
                .Attr("value", value);

            if (!formGroup)
                return AttachTimePiceker(input, format);

            HtmlStringBuilder sb = new HtmlStringBuilder();
            using (sb.Surround(AttachTimePiceker(new HtmlTag("div").Class("input-group time"), format)))
            {
                sb.Add(input);

                using (sb.Surround(new HtmlTag("span").Class("input-group-addon")))
                    sb.Add(new HtmlTag("span").Class("glyphicon glyphicon-time"));
            }

            return sb.ToHtml();
        }

        static HtmlTag AttachTimePiceker(HtmlTag tag, string format)
        {
            return tag.Attr("data-provide", "timepicker")
             .Attr("data-minute-step", "1")
             .Attr("data-second-step", "1")
             .Attr("data-show-meridian", (format.Contains("tt") || format.Contains("h")).ToString().ToLower())
             .Attr("data-show-seconds", (format.Contains("s")).ToString().ToLower())
             .Attr("data-hours-two-digits", (format.Contains("hh") || format.Contains("HH")).ToString().ToLower());
        }
    }
}
