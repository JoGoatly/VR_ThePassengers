using System.Collections.Generic;

public class Mail
{
    public string From;
    public string Subject;
    public string Body;
    public string Time;
    public bool Read;
}

/// <summary>Driver's inbox on the on-board computer.</summary>
public class MailBox
{
    public readonly List<Mail> Mails = new List<Mail>();

    public int UnreadCount
    {
        get
        {
            int n = 0;
            foreach (var m in Mails) if (!m.Read) n++;
            return n;
        }
    }

    public event System.Action<Mail> Received;

    public void Send(string from, string subject, string body, string time)
    {
        var mail = new Mail { From = from, Subject = subject, Body = body, Time = time };
        Mails.Insert(0, mail);
        Received?.Invoke(mail);
    }
}
