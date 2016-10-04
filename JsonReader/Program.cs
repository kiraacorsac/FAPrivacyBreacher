using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace JsonReader
{
    using InterestingStuff = Tuple<IEnumerable<TicketChain>, IEnumerable<Suspension>>;

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

    class TicketChain
    {
        public Ticket RootTicket { get; set; }
        public IEnumerable<TicketComment> TicketComments { get; set; }
    }

    class RepresentationGenerator
    {
        private static string shiftLines(string s) => s.Replace("\n", "\n    ");
        private static string toUTC(long unixseconds) => "[" + DateTimeOffset.FromUnixTimeSeconds(unixseconds).ToString("F") + "]";

        public static string GetSuspensionRepresentation(Suspension s)
        {
            return new StringBuilder().AppendFormat(
@"
===================================================================================
Suspension number: {0} == Suspended user: {1} == Enacted by: {2}
Created: {3} == Originally Lifted {4} == Lifted {5}
===================================================================================
>Public reason: 
    {6}
>Private reason: 
    {7}
>Admin reason:
    {8}
", s.row_id,
s.suspended_username,
s.enacted_username,
toUTC(s.created),
toUTC(s.time_lifted_orig),
toUTC(s.time_lifted),
shiftLines(s.reason_public),
shiftLines(s.reason_private),
shiftLines(s.reason_admin)).ToString();
        }

        public static string GetTicketChainRepresentation(TicketChain t)
        {
            var ret = new StringBuilder();
            ret.AppendFormat(
@"
===================================================================================
Ticket number: {0} == Submission date: {1} == Resolved: {2}
===================================================================================
Other notes: {5}
===================================================================================

>{1} - [{3}]: 
    {4} 
", t.RootTicket.rowid,
toUTC(t.RootTicket.ticketdate),
t.RootTicket.resolved == 1 ? "Yes" : "No",
t.RootTicket.username,
shiftLines(t.RootTicket.message),
t.RootTicket.other);

            t.TicketComments.ToList().ForEach(tc => ret.AppendFormat(
@"
>{0} - {1}: 
    {2}
", toUTC(tc.date),
tc.isstaff == 1 ? "<*"+tc.username+"*>" : "["+tc.username+"]",
shiftLines(tc.message)));
            return ret.ToString();
        }
    }

    class DBFilters
    {
        public static InterestingStuff FilterInteretestingStuff(InterestingStuff s, string name, string keyword)
        {
            return Tuple.Create(
                s.Item1.Where(chain => ContainsText(chain.RootTicket.message, keyword)
                                || chain.TicketComments.Any(tc => ContainsText(tc.message, keyword)))
                       .Where(chain => ContainsText(chain.RootTicket.username, name)
                                || ContainsText(chain.RootTicket.admin, name)
                                || chain.TicketComments.Any(tc => ContainsText(tc.username, name))),

                s.Item2.Where(su => ContainsText(su.enacted_username, name)
                               || ContainsText(su.suspended_username, name))
                       .Where(su => ContainsText(su.reason_admin, keyword)
                               || ContainsText(su.reason_private, keyword)
                               || ContainsText(su.reason_public, keyword))
                               );
        }

        private static bool ContainsText(string message, string messageSearchText)
        {
            return message.IndexOf(messageSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    class FAJson
    {
        public IEnumerable<Ticket> Tickets { get; set; }
        public IEnumerable<TicketComment> TicketComments { get; set; }
        public IEnumerable<TicketChain> TicketChains { get; set; }
        public IEnumerable<Suspension> Suspensions { get; set; }
        public InterestingStuff AllInterestingStuff => Tuple.Create(TicketChains, Suspensions);

        public FAJson(string path)
        {
            path += "\\";
            Tickets = ReadJsonLines<Ticket>(path + "tickets.jsons").ToList();
            TicketComments = ReadJsonLines<TicketComment>(path + "ticket_comments.jsons").ToList();
            Suspensions = ReadJsonLines<Suspension>(path + "suspensions.jsons").ToList();
            TicketChains = GenerateTicketChains();
        }


        private IEnumerable<TicketChain> GenerateTicketChains()
        {
            var commentsByTicket = TicketComments.ToLookup(tc => tc.ticketid);
            var result = new List<TicketChain>();
            foreach (var ticket in Tickets)
            {
                yield return new TicketChain
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
    }


    class Program
    {
        static void Main(string[] args)
        {
            InterestingStuff stuff;
            var jsonFileName = "crunchedFAdatabasedump.json";
            if (File.Exists(jsonFileName))
            {
                Console.WriteLine("crunchedFAdatabasedump.json found, not precrunching again. Loading the json...");
                using (var reader = new StreamReader(jsonFileName))
                {
                    stuff = JsonConvert.DeserializeObject<InterestingStuff>(reader.ReadToEnd());
                }
            }
            else
            {
                Console.WriteLine("Insert path to directory containing 'tickets', 'ticket_comments' and 'suspensions' .jsons files.");
                var path = Console.ReadLine();
                Console.WriteLine("Precrunching data...");
                var crunchedJson = new FAJson(path);
                stuff = crunchedJson.AllInterestingStuff;
                Console.WriteLine("Precrunching done, generating json dump for future lookups...");
                using (var writer = new StreamWriter(jsonFileName))
                {
                    writer.Write(JsonConvert.SerializeObject(stuff));
                }
                Console.WriteLine("Done.");
            }


            Console.WriteLine("Continue? [y/n]");
            while (Console.ReadLine() == "y")
            {
                Console.WriteLine("Insert name of user of interest, or leave blank for all users.");
                var name = Console.ReadLine();
                Console.WriteLine("Insert keyword of interest, or leave blank for all.");
                var keyword = Console.ReadLine();

                Console.WriteLine("Crunching...");
                var filteredReuslts = DBFilters.FilterInteretestingStuff(stuff, name, keyword);
                Console.WriteLine("Done crunching. Generating output...");
                using (var file = new StreamWriter("filtered_ticket_conversations.txt"))
                {
                    filteredReuslts.Item1
                        .Select(tc => RepresentationGenerator.GetTicketChainRepresentation(tc)).ToList()
                        .ForEach(tc => file.WriteLine(tc));
                }
                using (var file = new StreamWriter("filtered_suspensions.txt"))
                {
                    filteredReuslts.Item2
                        .Select(s => RepresentationGenerator.GetSuspensionRepresentation(s)).ToList()
                        .ForEach(s => file.WriteLine(s));
                }

                Console.WriteLine("Done.");
                Console.WriteLine("Continue? [y/n]");
            }

            Console.WriteLine("Done. Happy privacy breaching.");
            Console.ReadLine();
        }
    }
}
