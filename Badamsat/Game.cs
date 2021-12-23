using System.Text.Json;
using System.IO;
using System.Xml;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Identity;
using System.Text;

namespace Badamsat
{
    public class Card
    {
        public readonly int suit;
        public readonly int number;
        public static int[] possibleSuits = { 1, 2, 3, 4 };
        public static int[] possibleNumbers = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
        public static Card h7 = new Card(1, 7);

        public Card(int suit, int number)
        {
            this.suit = suit;
            this.number = number;
        }

        public override bool Equals(object card2)
        {
            var q = card2 as Card;
            return q.suit == suit && q.number == number;
        }

        public bool CheckProximity(Card othercard)
        {
            return Math.Abs(this.number - othercard.number) == 1;
        }

        public static int Compare(Card c1, Card c2)
        {
            return c1.number - c2.number;
        }

        public bool Equals(Card card2)
        {
            return this.number == card2.number && this.suit == card2.suit;
        }

        public string Xml()
        {
            return "<Card>" + suit.ToString() + " " + number.ToString() + "</Card>";
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }

    public class Hand
    {
        public readonly List<Card> cards;

        public Hand(List<Card> cards)
        {
            this.cards = cards;
        }

        public void Remove(int cardIndex)
        {
            cards.RemoveAt(cardIndex);
        }

        public static List<Hand> DealHands(int numPlayers)
        {
            int numDecks = numPlayers / 4 + 1;
            List<Card> deck = new List<Card>();
            foreach (var suit in Card.possibleSuits)
                foreach (var number in Card.possibleNumbers)
                    for (int i = 0; i < numDecks; i++)
                        deck.Add(new Card(suit, number));
            int currentIndex = deck.Count;
            int randomIndex;
            var r = new Random();
            while (currentIndex > 0)
            {
                currentIndex--;
                randomIndex = r.Next(currentIndex + 1);
                var value = deck[randomIndex];
                deck[randomIndex] = deck[currentIndex];
                deck[currentIndex] = value;
            }
            
            List<Hand> hands = new List<Hand>();
            int cardsPerHand = deck.Count / numPlayers;
            for (int i = 0; i < numPlayers; i++)
                hands.Add(new Hand(deck.GetRange(i * cardsPerHand, cardsPerHand)));

            int currentPlayerID = -1;
            for (int i = 0; i < numPlayers; i++)
                if (hands[i].cards.Contains(Card.h7))
                {
                    currentPlayerID = i;
                    break;
                }
            if (currentPlayerID == -1)
                return DealHands(numPlayers);
            for (int i = currentPlayerID; i < deck.Count % cardsPerHand; i = (i + 1) % numPlayers)
                hands[i].cards.Add(deck[numPlayers * cardsPerHand + i]);
            return hands;
        }

        public string Xml()
        {
            string stringified = "<Hand>";
            foreach (var card in this.cards)
                stringified += card.Xml();
            stringified += "</Hand>";
            return stringified;
        }
    }

    public class Pile
    {
        public bool complete;
        public List<Card> cards;

        public Pile()
        {
            cards = new List<Card>();
            complete = false;
        }

        public void Sort()
        {
            cards.Sort(Card.Compare);
        }

        public bool CanAdd(Card card, bool isFirstPile)
        {
            if (complete)
                return false;
            foreach (Card card2 in cards)
            {
                if (card.Equals(card2))
                    return false;
            }
            foreach (Card card2 in cards)
                if (card.CheckProximity(card2) && card.suit == card2.suit)
                    return true;
            if (this.cards.Count == 0 && card.number == 7 && (card.suit == 1 || !isFirstPile))
                return true;
            return false;
        }

        public bool Add(Card card, bool isFirstPile)
        {
            if (this.CanAdd(card, isFirstPile))
            {
                this.cards.Add(card);
                this.Sort();
            }
            if (this.cards.Count == 13)
                this.complete = true;
            return this.CanAdd(card, isFirstPile);
        }

        public string Xml()
        {
            string stringified = "<Pile>";
            foreach (var card in this.cards)
                stringified += card.Xml();
            stringified += "</Pile>";
            return stringified;
        }
    }

    public class Game
    {
        public List<Pile> piles;
        public List<Hand> hands;
        public List<string> usernames;
        public int currentPlayerID;
        public int state;
        public DateTime savedAt;
        public int numPlayers => usernames.Count;

        private static string gameLocation = "wwwroot/game.xml";

        public Game(List<string> usernames, bool deal)
        {
            this.usernames = usernames;
            if (deal)
            {
                currentPlayerID = -1;
                while (currentPlayerID == -1)
                {
                    this.hands = Hand.DealHands(numPlayers);
                    for (int i = 0; i < numPlayers; i++)
                        if (this.hands[i].cards.Contains(Card.h7))
                        {
                            currentPlayerID = i;
                            break;
                        }
                }
            }
            else
            {
                this.hands = new List<Hand>();
            }
            this.piles = new List<Pile>();
            state = 1;
            this.savedAt = DateTime.Now;
        }

        public string Stringify()
        {
            string stringified = "<?xml version = \"1.0\" encoding = \"utf-8\" ?>";
            stringified += "<Game>";
            stringified += "<CurrentPlayerID>" + currentPlayerID.ToString() + "</CurrentPlayerID>";
            stringified += "<State>" + state.ToString() + "</State>";
            stringified += "<SavedAt>" + this.savedAt.ToString() + "</SavedAt>";
            foreach (var username in usernames)
                stringified += "<Username>" + username + "</Username>";
            foreach (var hand in hands)
                stringified += hand.Xml();
            foreach (var pile in piles)
                stringified += pile.Xml();
            stringified += "</Game>";
            return stringified;
        }

        public static Game Destringify(string encoded)
        {
            XmlDocument xDoc = new XmlDocument();
            xDoc.LoadXml(encoded);
            var tempUsernames = new List<string>();
            int tempCurrentID = -1;
            var tempHands = new List<Hand>();
            var tempPiles = new List<Pile>();
            var tempState = -1;
            var tempDate = new DateTime();
            for (int i = 0; i < xDoc.LastChild.ChildNodes.Count; i++)
            {
                var element = xDoc.DocumentElement.ChildNodes.Item(i);
                if (element.Name == "Pile")
                {
                    var tempCards = new List<Card>();
                    for (int j = 0; j < element.ChildNodes.Count; j++)
                    {
                        var cardval = element.ChildNodes.Item(j).InnerText.Split(" ");
                        tempCards.Add(new Card(Convert.ToInt32(cardval[0]), Convert.ToInt32(cardval[1])));
                    }
                    tempPiles.Add(new Pile());
                    tempPiles.Last().cards = tempCards;
                }
                if (element.Name == "Hand")
                {
                    var tempCards = new List<Card>();
                    for (int j = 0; j < element.ChildNodes.Count; j++)
                    {
                        var cardval = element.ChildNodes.Item(j).InnerText.Split(" ");
                        tempCards.Add(new Card(Convert.ToInt32(cardval[0]), Convert.ToInt32(cardval[1])));
                    }
                    tempHands.Add(new Hand(tempCards));
                }
                if (element.Name == "Username")
                {
                    tempUsernames.Add(element.InnerText);
                }
                if (element.Name == "CurrentPlayerID")
                {
                    tempCurrentID = Convert.ToInt32(element.InnerText);
                }
                if (element.Name == "State")
                    tempState = Convert.ToInt32(element.InnerText);
                if (element.Name == "SavedAt")
                    tempDate = Convert.ToDateTime(element.InnerText);
            }
            Game newGame = new(tempUsernames, false);
            newGame.currentPlayerID = tempCurrentID;
            newGame.hands = tempHands;
            newGame.piles = tempPiles;
            newGame.state = tempState;
            newGame.savedAt = tempDate;
            return newGame;
        }

        public void Save()
        {
            File.WriteAllText(gameLocation, this.Stringify());
        }

        public static Game LoadOrCreate()
        {
            if (!File.Exists(gameLocation))
            {
                var newgame = new Game(new List<string>(), false);
                newgame.state = 0;
                newgame.Save();
                return newgame;
            }
            var g = Destringify(File.ReadAllText(gameLocation));
            if ((DateTime.Now - g.savedAt).TotalMinutes > 30)
            {
                g = new Game(new List<string>(), false);
                g.state = 0;
                g.Save();
            }
            return g;
        }

        public void Delete()
        {
            File.Delete(gameLocation);
        }

        public bool MakePile(Card startCard)
        {
            if (startCard.number == 7)
            {
                piles.Add(new Pile());
                piles.Last().Add(startCard, piles.Count == 1);
                return true;
            }
            return false;
        }

        public List<List<int>> AvailablePlays(int userID)
        {
            List<List<int>> options = new List<List<int>>();
            foreach (Card card in this.hands[userID].cards)
            {
                options.Add(new List<int>());
                for (int i = 0; i < this.piles.Count; i++)
                {
                    if (this.piles[i].CanAdd(card, false))
                        options.Last().Add(i);
                }
                if (card.number == 7 && (this.piles.Count > 0 || card.suit == 1))
                    options.Last().Add(this.piles.Count);
            }
            return options;
        }

        public bool HasPlays(int userID)
        {
            var options = AvailablePlays(userID);
            foreach (var q in options)
            {
                if (q.Count > 0)
                    return true;
            }
            return false;
        }

        public bool Turn(int userIndex, int cardIndex, int pileIndex)
        {
            if (pileIndex == this.piles.Count)
            {
                this.MakePile(this.hands[userIndex].cards[cardIndex]);
                this.currentPlayerID = (currentPlayerID + 1) % this.numPlayers;
                this.hands[userIndex].Remove(cardIndex);
                return false;
            }
            this.piles[pileIndex].Add(this.hands[userIndex].cards[cardIndex], false);
            this.hands[userIndex].Remove(cardIndex);
            if (this.hands[userIndex].cards.Count == 0)
            {
                state = 2;
                return true;
            }
            currentPlayerID = (currentPlayerID + 1) % this.numPlayers;
            return false;
        }
    }
}
