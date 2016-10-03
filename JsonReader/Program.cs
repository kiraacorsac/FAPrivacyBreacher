using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonReader
{
    public class Ticket
    {
        public string username { get; set; }
        public int resolved { get; set; }
        public string admin { get; set; }
        public int assigned_to_user_id { get; set; }
        public int userid { get; set; }
        public long lastlookedat { get; set; }
        public string assigned_username { get; set; }
        public string other { get; set; }
        public int rowid { get; set; }
        public int replies { get; set; }
        public int issuetype { get; set; }
        public string message { get; set; }
        public string uk_username { get; set; }
        public long ticketdate { get; set; }
    }

    public class TicketComment
    {
        public string username { get; set; }
        public int userid { get; set; }
        public int isstaff { get; set; }
        public int rowid { get; set; }
        public string message { get; set; }
        public long date { get; set; }
        public int ticketid { get; set; }
        public string uk_username { get; set; }
    }

    public class Suspension
    {
        public string reason_public { get; set; }
        public string reason_admin { get; set; }
        public int enacted_by { get; set; }
        public long created { get; set; }
        public long time_lifted_orig { get; set; }
        public string enacted_username { get; set; }
        public long time_lifted { get; set; }
        public string suspended_username { get; set; }
        public int row_id { get; set; }
        public int user_suspended { get; set; }
        public string reason_private { get; set; }
    }

    class CommentTuple
    {
        public Ticket RootTicket { get; set; }
        public IEnumerable<TicketComment> TicketComments { get; set; }
    }

    class FAJson
    {
        public IEnumerable<Ticket> Tickets { get; set; }
        public IEnumerable<TicketComment> TicketComments { get; set; }
        public IEnumerable<CommentTuple> TicketChains { get; set; }
        public IEnumerable<Suspension> Suspensions { get; set; }

        public FAJson(string path)
        {
            path += "\\";
            Tickets = ReadJsonLines<Ticket>(path +  "tickets.jsons").ToList();
            TicketComments = ReadJsonLines<TicketComment>(path + "ticket_comments.jsons").ToList();
            Suspensions = ReadJsonLines<Suspension>(path + "suspensions.jsons").ToList();
            TicketChains = GenerateTicketChains();
        }

        public IEnumerable<CommentTuple> FilteredTicketChains(string name = "", string message = "")
        {

            return TicketChains.Where(tuple => ContainsText(tuple.RootTicket.message, message)
                                || tuple.TicketComments.Any(tc => ContainsText(tc.message, message)))
                               .Where(tuple => ContainsText(tuple.RootTicket.username, name)
                                || ContainsText(tuple.RootTicket.admin, name)
                                || tuple.TicketComments.Any(tc => ContainsText(tc.username, name)));
        }

        public IEnumerable<Suspension> FilteredSuspensions(string name = "", string reason = "")
        {
            return Suspensions.Where(su => ContainsText(su.enacted_username, name)
                               || ContainsText(su.suspended_username, name))
                              .Where(su => ContainsText(su.reason_admin, reason)
                               || ContainsText(su.reason_private, reason)
                               || ContainsText(su.reason_public, reason));
        }

        private IEnumerable<CommentTuple> GenerateTicketChains()
        {
            var commentsByTicket = TicketComments.ToLookup(tc => tc.ticketid);
            var result = new List<CommentTuple>();
            foreach (var ticket in Tickets)
            {
                yield return new CommentTuple
                {
                    RootTicket = ticket,
                    TicketComments = commentsByTicket.Contains(ticket.rowid)
                        ? commentsByTicket[ticket.rowid].OrderBy(tc => tc.date).ToList()
                        : new List<TicketComment>()
                };
            }
        }

        private IEnumerable<T> ReadJsonLines<T>(string path)
        {
            using (var reader = new StreamReader(path))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    yield return JsonConvert.DeserializeObject<T>(line);
                }
            }
        }

        private bool ContainsText(string message, string messageSearchText)
        {
            return message.IndexOf(messageSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Insert path to directory containing 'tickets', 'ticket_comments' and 'suspensions' .jsons files.");
            var path = Console.ReadLine();
            Console.WriteLine("Precrunching data...");
            try
            {
                var jsonReader = new FAJson(path);
                Console.WriteLine("Insert name of user of interest, or leave blank for all users.");
                var name = Console.ReadLine();
                Console.WriteLine("Insert keyword of interest, or leave blank for all.");
                var keyword = Console.ReadLine();

                Console.WriteLine("Crunching tickets...");
                var ticketResults = jsonReader.FilteredTicketChains(name: name, message: keyword);
                Console.WriteLine("Crunching suspensions...");
                var suspensionResults = jsonReader.FilteredSuspensions(name: name, reason: keyword);
                Console.WriteLine("Done crunching. Generating output...");
                using (var file = new StreamWriter("filtered_ticket_conversations.json"))
                {
                    file.Write(JsonConvert.SerializeObject(ticketResults, Formatting.Indented));
                }
                using (var file = new StreamWriter("filtered_suspensions.json"))
                {
                    file.Write(JsonConvert.SerializeObject(suspensionResults, Formatting.Indented));
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Files not found. Make sure the directory contains all files needed.");
                Console.ReadLine();
                Environment.Exit(1);
            }

            Console.WriteLine("Done. Happy privacy breaching.");
            Console.ReadLine();
        }
    }
}
