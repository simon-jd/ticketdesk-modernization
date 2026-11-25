// LegacyRender.cs - the legacy rendering (TicketExtensions.HtmlDetails / TicketEventExtensions.HtmlComment of TicketDesk 2.5)
// run on the lab VM against the legacy site's own assemblies (.NET Framework 4.8, C# 5 compiler that ships with Windows):
//   csc /r:MarkdownSharp.dll /r:HtmlSanitizer.dll /r:AngleSharp.dll LegacyRender.cs   (DLLs from the site's bin folder)
//   LegacyRender.exe corpus.tsv legacy-out.tsv
// Input lines: id TAB isHtml TAB base64(text)|-   Output lines: id TAB OK|ERR TAB base64(html or error)
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

class LegacyRender
{
    static int Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
        using (var w = new StreamWriter(args[1], false, new UTF8Encoding(false)))
        {
            w.NewLine = "\n";
            foreach (var line in File.ReadAllLines(args[0]))
            {
                var p = line.Split('\t');
                var text = p[2] == "-" ? null : Encoding.UTF8.GetString(Convert.FromBase64String(p[2]));
                try
                {
                    var content = p[1] == "1" ? text : new MarkdownSharp.Markdown(new MarkdownSharp.MarkdownOptions
                    {
                        AutoHyperlink = true, AutoNewLines = true, EncodeProblemUrlCharacters = true, LinkEmails = true
                    }).Transform(text);
                    var html = new Ganss.XSS.HtmlSanitizer().Sanitize(content);
                    w.WriteLine(p[0] + "\tOK\t" + Convert.ToBase64String(Encoding.UTF8.GetBytes(html)));
                }
                catch (Exception e)
                {
                    w.WriteLine(p[0] + "\tERR\t" + Convert.ToBase64String(Encoding.UTF8.GetBytes(e.GetType().FullName + ": " + e.Message)));
                }
            }
        }
        return 0;
    }
}
