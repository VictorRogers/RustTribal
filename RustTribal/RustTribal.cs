
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using JetBrains.Annotations;

    using Network;

    using Oxide.Game.Rust.Libraries.Covalence;


    using UnityEngine;

    using Oxide.Core.Libraries.Covalence;

    using Oxide.Core;

namespace Oxide.Plugins
{

    [Info("RustTribal", "*Vic", 0.1)]
    [Description("Rust Tribal")]
    public class RustTribal : RustPlugin
    {
        #region Oxide Members

        private ChatMenu chatMenu;
        private Game game;
        private GateWay gateWay;

        #endregion Oxide Members

        #region Oxide Hooks

        [UsedImplicitly]
        private string CanClientLogin(Connection packet)
        {
            var message = gateWay.IsClientAuthorized(packet, game);
            if (message.Response == AuthMessage.ResponseType.Rejected)
            {
                return message.GetMessage();
            }
            else
            {
                //Todo: Send client a welcome message
                return null;
            }
        }

        [UsedImplicitly]
        private void Loaded()
        {
            Puts("Plugin loaded");
        }

        [UsedImplicitly]
        private void OnPlayerConnected(Message packet)
        {
            var userName = packet.connection.username;
            var userId = packet.connection.userid;
            game.IncomingPlayer(userId, userName);
        }

        /// <summary>
        /// Called when the server is initialized.
        /// Loads game data if data exists.
        /// Creates a new game if no data exists.
        /// </summary>
        [UsedImplicitly]
        private void OnServerInitialized()
        {
            Puts("Server init!");
            game = InitGame();
            gateWay = new GateWay();
            chatMenu = new ChatMenu();
            game.Save();
        }

        private Game InitGame()
        {
            return Interface.Oxide.DataFileSystem.ExistsDatafile("RustTribalGameData")
                ? Interface.Oxide.DataFileSystem.ReadObject<Game>("RustTribalGameData")
                : new Game();
        }

        /// <summary>
        /// Called upon the server saving.
        /// Saves the game object to disk.
        /// </summary>
        [UsedImplicitly]
        private void OnServerSave()
        {
            game.Save();
        }

        /// <summary>
        /// Called upon the plugin being unloaded.
        /// Saves the game object to disk.
        /// </summary>
        [UsedImplicitly]
        private void Unload()
        {
            game.Save();
        }

        #endregion Oxide Hooks

        #region Oxide Chat Hooks

        [ChatCommand("test")]
        private void Testing(BasePlayer player, string command, string[] args)
        {
            chatMenu.TestChat(player, command, args);
        }

        #endregion Oxide Chat Hooks

        #region Chat Menu Class

        public class ChatMenu : RustTribal
        {
            public void TestChat(BasePlayer player, string command, string[] args)
            {
                SendReply(player, "It worked.");
            }
        }

        #endregion Chat Menu Class

        #region Game Class

        public class Game
        {
            #region Game Members

            /// <summary>
            /// Identifier for the Rust Tribal Game Instance
            /// </summary>
            private Guid gameId;

            private World world;

            #endregion Game Members

            #region Game Properties

            public bool IsWorldPopulating => world.IsWorldPopulating;

            #endregion

            #region Game Constructors

            public Game()
            {
                gameId = Guid.NewGuid();
                world = new World();
            }

            #endregion Game Constructors

            #region Game Methods

            /// <summary>
            /// Writes the Game object to disk.
            /// </summary>
            public void Save() => Interface.Oxide.DataFileSystem.WriteObject("RustTribalGameData", this);

            #endregion

            public bool IsPlayerCorrectGender(Person.PlayerGender gender)
            {
                if (!world.IsWorldPopulating)
                {
                    return false;
                }

                var tribe = world.FindPopulatingTribe();

                return (gender == Person.PlayerGender.Female && tribe.IsFemalesPopulating)
                       || (gender == Person.PlayerGender.Male && tribe.IsMalesPopulating);
            }

            public bool IsPlayerKnown(ulong id)
            {
                world.FindPersonById(id);
                return false;
            }

            public bool IsBirthPlaceAvailable()
            {
                throw new NotImplementedException();
            }

            public bool IsPlayerAlive(ulong id)
            {
                var isAlive = false;
                var player = world.FindPersonById(id);
                if ((player != null) && player.IsAlive)
                {
                    isAlive = true;
                }

                return isAlive;
            }

            public void IncomingPlayer(ulong userId, string userName)
            {
                //The player should already be added if they are alive
                if (!IsPlayerAlive(userId))
                {
                    world.AddNewPerson(userId, userName);
                }
            }
        }

        #endregion Game Class

        #region World Class

        public class World
        {
            private Queue<int> birthPlaces;

            //Todo: Config Value
            private const int MaxInitialTribes = 2;

            //Todo: Config Value
            private const int MaxServerPopulation = 50;

            public bool IsWorldPopulating => tribes.Any(x => x.IsTribePopulating)
                && tribes.Count < MaxInitialTribes;

            private int ServerPopulationLimit => birthPlaces.Count() +
                persons.Count(x => { return new RustPlayerManager().Connected.Any(r => r.Id == x.Id); });

            private List<Person> persons;

            private List<Tribe> tribes;


            public World()
            {
                persons = new List<Person>();
                tribes = new List<Tribe>();

                AddNewTribe("Alpha");
                AddNewTribe("Bravo");
            }

            public Person FindPersonById(ulong id) => persons.FirstOrDefault(x => x.Id == id.ToString());

            public Tribe FindPopulatingTribe() => tribes.FirstOrDefault(x => x.IsTribePopulating);

            public void AddNewPerson(ulong userId, string userName)
            {
                var newPerson = new Person(userId);
                persons.Add(newPerson);
                //Todo: Add logging here
                FindPopulatingTribe().AddNewMember(newPerson);
            }

            public void AddNewTribe(string newTribeName)
            {
                var newTribe = new Tribe(newTribeName);
                tribes.Add(newTribe);
            }

        }

        #endregion World Class

        #region GateWay Class

        public class GateWay
        {
            /// <summary>
            /// Determines if a client may connect or not
            /// </summary>
            /// <param name="packet">The client connection packet</param>
            /// <param name="game">The Rust Tribal Game object</param>
            /// <returns>Returns true if authorized, false if rejected</returns>
            public AuthMessage IsClientAuthorized(Connection packet, Game game)
            {
                AuthMessage authMessage = null;
                var id = packet.userid;

                if (game.IsPlayerKnown(id) && game.IsPlayerAlive(id))
                {
                    authMessage = new AuthMessage(
                        AuthMessage.ResponseType.Accepted,
                        "Welcome back to Rust Tribal.");
                }
                else if (game.IsWorldPopulating)
                {
                    if (game.IsPlayerCorrectGender(
                            packet.player.gameObject.ToBaseEntity().ToPlayer().playerModel.IsFemale
                                ? Person.PlayerGender.Female
                                : Person.PlayerGender.Male))
                    {
                        authMessage = new AuthMessage(
                            AuthMessage.ResponseType.Rejected,
                            "The game is currently populating the world and "
                            + "there are too many players of your gender");
                    }
                    else
                    {
                        authMessage = new AuthMessage(
                            AuthMessage.ResponseType.Accepted,
                            "Welcome to Rust Tribal.");
                    }
                }
                else if (game.IsBirthPlaceAvailable())
                {
                    authMessage = new AuthMessage(
                        AuthMessage.ResponseType.Accepted,
                        "Welcome to Rust Tribal.");
                }
                else
                {
                    authMessage = new AuthMessage(
                        AuthMessage.ResponseType.Rejected,
                        "There are currently no spawn points available.\n Visit RustTribal.com to join the queue.");
                }

                return authMessage;
            }
        }

        #endregion GateWay Class

        #region Tribe Class

        public class Tribe
        {
            //Todo: Config Value
            private const int maxInitialTribalMembers = 4;

            //Todo: Config Value
            private const int maxInitialMales = 2;

            //Todo: Config Value
            private const int maxInitialFemales = 2;

            public bool IsTribePopulating => (NumMales < maxInitialMales) && (NumFemales < maxInitialFemales);

            public bool IsMalesPopulating => (NumMales < maxInitialMales);

            public bool IsFemalesPopulating => (NumFemales < maxInitialFemales);

            private int NumMales => members.Count(x => x.Gender == Person.PlayerGender.Male);

            private int NumFemales => members.Count(x => x.Gender == Person.PlayerGender.Female);

            private string tribeName;

            private List<Person> members;

            public Tribe(string newTribeName)
            {
                members = new List<Person>();
                tribeName = newTribeName;
            }

            public void AddNewMember(Person newMember)
            {
                members.Add(newMember);
            }
        }

        #endregion Tribe Class

        #region Person Class

        public class Person
        {
            private Demeanor demeanor;

            //Todo: Set Gender
            public PlayerGender Gender
            {
                get
                {
                    var bPlayer = (BasePlayer)new RustPlayerManager().FindPlayerById(Id).Object;
                    return bPlayer.playerModel.IsFemale ? PlayerGender.Female : PlayerGender.Male;
                }
            }

            public bool IsAlive { get; private set; }

            public string Id { get; private set; }

            //Todo: Needs testing to understand functionality

            public Person(ulong userId)
            {
                Id = userId.ToString();
            }

            private enum Demeanor
            {
                Psychotic,
                Troubled ,
                Neutral,
                Warm,
                Friendly
            }

            public enum PlayerGender
            {
                Male,
                Female
            }
        }

        #endregion Person Class

        #region Auth Message

        public class AuthMessage
        {
            public ResponseType Response { get; private set; }
            public string Reason { get; private set; }

            public enum ResponseType
            {
                Accepted,
                Rejected
            }

            public AuthMessage(ResponseType type, string reason)
            {
                Response = type;
                Reason = reason;
            }

            public string GetMessage() => $"{Response}: {Reason}.";
        }

        #endregion
    }

}