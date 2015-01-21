using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;

namespace RestrictionGroupConfig
{
    class Account
    {
        public string Code;
        public string Description;
        public HashSet<string> Subaccounts = new HashSet<string>();
    }

    class RestrictionGroup
    {
        public List<string> Accounts = new List<string>();
        public List<string> Subaccounts = new List<string>();
        public string Code;
        public string Description;
    }

    class Program
    {
        static void Main(string[] args)
        {
            var combinations = ProcessFile("Combinations.csv");
            var groups = GetRestrictionGroupsForCombinations(combinations);

            Console.WriteLine("Total groups needed: " + groups.Count);
            Console.WriteLine("Most accounts in a group: " + groups.Max(g => g.Accounts.Count));
            Console.WriteLine("Most subaccounts in a group: " + groups.Max(g => g.Subaccounts.Count));

            var accounts = GetAccountsList();
            var subs = GetSubsList();
            
            var screen = new GL104000.Screen();
            screen.CookieContainer = new System.Net.CookieContainer();
            screen.Login(Properties.Settings.Default.Login, Properties.Settings.Default.Password);
            var schema = screen.GetSchema();

            foreach (var account in combinations.Values)
            {
                Console.WriteLine("Processing group {0} ({1})", account.Code, account.Description);

                var commands = new List<GL104000.Command>();
                commands.Add(new GL104000.Value() { LinkedCommand = schema.RestrictionGroup.GroupName, Value = account.Code });
                commands.Add(new GL104000.Value() { LinkedCommand = schema.RestrictionGroup.Description, Value = account.Description });

                if (!accounts.Contains(account.Code))
                {
                    Console.WriteLine("ERROR: Account {0} does not exist.", account.Code);
                    continue;
                }

                commands.Add(new GL104000.Value() { LinkedCommand = schema.Accounts.Account, Value = account.Code });
                commands.Add(new GL104000.Value() { LinkedCommand = schema.Accounts.Included, Value = "True", Commit = true });

                foreach (string sub in account.Subaccounts)
                {
                    if (subs.Contains(sub))
                    {
                        commands.Add(new GL104000.Value() { LinkedCommand = schema.Subaccounts.Subaccount, Value = sub });
                        commands.Add(new GL104000.Value() { LinkedCommand = schema.Subaccounts.Included, Value = "True", Commit = true });
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Sub {0} does not exist.", sub);
                    }
                }

                commands.Add(schema.Actions.Save);
                screen.Clear();
                screen.Submit(commands.ToArray());
            }

            screen.Logout();

            Console.WriteLine("Completed.");
            Console.ReadLine();
        }

        private static HashSet<string> GetAccountsList()
        {
            var screen = new GL202500.Screen();
            screen.CookieContainer = new System.Net.CookieContainer();
            screen.Login(Properties.Settings.Default.Login, Properties.Settings.Default.Password);

            var schema = screen.GetSchema();
            var result = screen.Export(new GL202500.Command[] { schema.AccountRecords.Account }, null, 0, false, false);
            screen.Logout();

            var accounts = new HashSet<string>();

            for (int i = 0; i < result.Length; i++ )
            {
                accounts.Add(result[i][0]);
            }
            return accounts;
        }

        private static HashSet<string> GetSubsList()
        {
            var screen = new GL203000.Screen();
            screen.CookieContainer = new System.Net.CookieContainer();
            screen.Login(Properties.Settings.Default.Login, Properties.Settings.Default.Password);

            var schema = screen.GetSchema();
            var result = screen.Export(new GL203000.Command[] { schema.SubRecords.Subaccount}, null, 0, false, false);
            screen.Logout();

            var subs = new HashSet<string>();

            for (int i = 0; i < result.Length; i++)
            {
                subs.Add(result[i][0]);
            }
            return subs;
        }

        private static Dictionary<string, Account> ProcessFile(string path)
        {
            var combinations = new Dictionary<string, Account>();

            using (TextFieldParser parser = new TextFieldParser(path))
            {
                parser.SetDelimiters(",", ";");
                parser.HasFieldsEnclosedInQuotes = true;
                while (!parser.EndOfData)
                {
                    string[] fields = parser.ReadFields();
                    
                    Account account;
                    if(!combinations.TryGetValue(fields[0], out account))
                    {
                        account = new Account()
                        {
                            Code = fields[0],
                            Description = fields[2]
                        };
                        combinations.Add(account.Code, account);
                    }
                    account.Subaccounts.Add(fields[1]);
                }
            }

            return combinations;
        }

        private static List<RestrictionGroup> GetRestrictionGroupsForCombinations(Dictionary<string, Account> combinations)
        {
            var groups = new List<RestrictionGroup>();
            var processedAccounts = new HashSet<string>();

            foreach (Account account in combinations.Values)
            {
                if (processedAccounts.Contains(account.Code)) continue;
                processedAccounts.Add(account.Code);

                var group = new RestrictionGroup();
                groups.Add(group);
                group.Accounts.Add(account.Code);
                group.Subaccounts.AddRange(account.Subaccounts);

                // Try to find any other account which has same subaccount combination
                bool foundMatch = false;
                foreach (Account potentialMatch in combinations.Values)
                {
                    if (processedAccounts.Contains(potentialMatch.Code)) continue;
                    if (account.Subaccounts.SequenceEqual(potentialMatch.Subaccounts))
                    {
                        foundMatch = true;
                        group.Accounts.Add(potentialMatch.Code);
                        processedAccounts.Add(potentialMatch.Code);
                    }
                }

                if (foundMatch)
                {
                    group.Code = account.Code;
                    group.Description = "Access for " + String.Join("-", group.Accounts);

                    if(group.Description.Length > 255)
                    {
                        group.Description = group.Description.Substring(0, 252) + "...";
                    }
                }
                else
                {
                    group.Code = account.Code;
                    group.Description = account.Description;
                }
            }

            return groups;
        }
    }
}