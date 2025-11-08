using Leopardy.Models;

namespace Leopardy.Services;

public static class GameDataService
{
    public static List<GameTemplate> GetGameTemplates()
    {
        return new List<GameTemplate>
        {
            new GameTemplate
            {
                Name = "Science & Technology",
                Categories = new List<Category>
                {
                    new Category
                    {
                        Name = "Computers",
                        Clues = new List<Clue>
                        {
                            new Clue { Question = "This programming language was created by Guido van Rossum", Answer = "Python", Value = 200 },
                            new Clue { Question = "This company created the iPhone", Answer = "Apple", Value = 400 },
                            new Clue { Question = "This is the largest search engine", Answer = "Google", Value = 600 },
                            new Clue { Question = "This protocol is used to transfer web pages", Answer = "HTTP", Value = 800 },
                            new Clue { Question = "This is the name of Microsoft's operating system", Answer = "Windows", Value = 1000 }
                        }
                    },
                    new Category
                    {
                        Name = "Space",
                        Clues = new List<Clue>
                        {
                            new Clue { Question = "This is the largest planet in our solar system", Answer = "Jupiter", Value = 200 },
                            new Clue { Question = "This is the closest star to Earth", Answer = "The Sun", Value = 400 },
                            new Clue { Question = "This is the name of the first human to walk on the moon", Answer = "Neil Armstrong", Value = 600 },
                            new Clue { Question = "This is the name of our galaxy", Answer = "The Milky Way", Value = 800 },
                            new Clue { Question = "This is the name of the space telescope launched in 2021", Answer = "James Webb Space Telescope", Value = 1000 }
                        }
                    },
                    new Category
                    {
                        Name = "Chemistry",
                        Clues = new List<Clue>
                        {
                            new Clue { Question = "This is the chemical symbol for water", Answer = "H2O", Value = 200 },
                            new Clue { Question = "This is the most abundant gas in Earth's atmosphere", Answer = "Nitrogen", Value = 400 },
                            new Clue { Question = "This is the atomic number of carbon", Answer = "6", Value = 600 },
                            new Clue { Question = "This process converts sugar to alcohol", Answer = "Fermentation", Value = 800 },
                            new Clue { Question = "This is the name of the table that organizes all elements", Answer = "The Periodic Table", Value = 1000 }
                        }
                    },
                    new Category
                    {
                        Name = "Biology",
                        Clues = new List<Clue>
                        {
                            new Clue { Question = "This is the powerhouse of the cell", Answer = "Mitochondria", Value = 200 },
                            new Clue { Question = "This is the largest organ in the human body", Answer = "The Skin", Value = 400 },
                            new Clue { Question = "This is the process by which plants make food", Answer = "Photosynthesis", Value = 600 },
                            new Clue { Question = "This is the study of heredity", Answer = "Genetics", Value = 800 },
                            new Clue { Question = "This is the number of chromosomes in a human cell", Answer = "46", Value = 1000 }
                        }
                    },
                    new Category
                    {
                        Name = "Mathematics",
                        Clues = new List<Clue>
                        {
                            new Clue { Question = "This is the value of Pi to two decimal places", Answer = "3.14", Value = 200 },
                            new Clue { Question = "This is the square root of 144", Answer = "12", Value = 400 },
                            new Clue { Question = "This is the sum of angles in a triangle", Answer = "180 degrees", Value = 600 },
                            new Clue { Question = "This is the name of a shape with 8 sides", Answer = "Octagon", Value = 800 },
                            new Clue { Question = "This is the mathematical constant approximately equal to 2.718", Answer = "e", Value = 1000 }
                        }
                    },
                    new Category
                    {
                        Name = "Physics",
                        Clues = new List<Clue>
                        {
                            new Clue { Question = "This is the speed of light in a vacuum", Answer = "299,792,458 meters per second", Value = 200 },
                            new Clue { Question = "This is the force that pulls objects toward Earth", Answer = "Gravity", Value = 400 },
                            new Clue { Question = "This is the unit of measurement for force", Answer = "Newton", Value = 600 },
                            new Clue { Question = "This is the name of Einstein's famous equation", Answer = "E=mc²", Value = 800 },
                            new Clue { Question = "This is the law that states energy cannot be created or destroyed", Answer = "Conservation of Energy", Value = 1000 }
                        }
                    }
                }
            },
            new GameTemplate
            {
                Name = "History & Geography",
                Categories = new List<Category>
                {
                    new Category
                    {
                        Name = "World Capitals",
                        Clues = new List<Clue>
                        {
                            new Clue { Question = "This is the capital of France", Answer = "Paris", Value = 200 },
                            new Clue { Question = "This is the capital of Japan", Answer = "Tokyo", Value = 400 },
                            new Clue { Question = "This is the capital of Australia", Answer = "Canberra", Value = 600 },
                            new Clue { Question = "This is the capital of Brazil", Answer = "Brasília", Value = 800 },
                            new Clue { Question = "This is the capital of South Africa", Answer = "Cape Town", Value = 1000 }
                        }
                    },
                    new Category
                    {
                        Name = "US Presidents",
                        Clues = new List<Clue>
                        {
                            new Clue { Question = "This was the first President of the United States", Answer = "George Washington", Value = 200 },
                            new Clue { Question = "This President issued the Emancipation Proclamation", Answer = "Abraham Lincoln", Value = 400 },
                            new Clue { Question = "This President was in office during World War II", Answer = "Franklin D. Roosevelt", Value = 600 },
                            new Clue { Question = "This President served two non-consecutive terms", Answer = "Grover Cleveland", Value = 800 },
                            new Clue { Question = "This President was known as 'The Great Communicator'", Answer = "Ronald Reagan", Value = 1000 }
                        }
                    },
                    new Category
                    {
                        Name = "Ancient Civilizations",
                        Clues = new List<Clue>
                        {
                            new Clue { Question = "This ancient civilization built the pyramids", Answer = "The Egyptians", Value = 200 },
                            new Clue { Question = "This was the capital of the Roman Empire", Answer = "Rome", Value = 400 },
                            new Clue { Question = "This civilization lived in Central America", Answer = "The Mayans", Value = 600 },
                            new Clue { Question = "This was the first emperor of China", Answer = "Qin Shi Huang", Value = 800 },
                            new Clue { Question = "This ancient city was destroyed by a volcano", Answer = "Pompeii", Value = 1000 }
                        }
                    },
                    new Category
                    {
                        Name = "World Wars",
                        Clues = new List<Clue>
                        {
                            new Clue { Question = "World War I began in this year", Answer = "1914", Value = 200 },
                            new Clue { Question = "This event triggered World War I", Answer = "The assassination of Archduke Franz Ferdinand", Value = 400 },
                            new Clue { Question = "This was the code name for the D-Day invasion", Answer = "Operation Overlord", Value = 600 },
                            new Clue { Question = "This country surrendered to end World War II in Europe", Answer = "Germany", Value = 800 },
                            new Clue { Question = "This was the name of the atomic bomb dropped on Hiroshima", Answer = "Little Boy", Value = 1000 }
                        }
                    },
                    new Category
                    {
                        Name = "European History",
                        Clues = new List<Clue>
                        {
                            new Clue { Question = "This was the period of renewed interest in art and learning", Answer = "The Renaissance", Value = 200 },
                            new Clue { Question = "This revolution began in France in 1789", Answer = "The French Revolution", Value = 400 },
                            new Clue { Question = "This was the name of the wall that divided Berlin", Answer = "The Berlin Wall", Value = 600 },
                            new Clue { Question = "This empire was known as 'The Sick Man of Europe'", Answer = "The Ottoman Empire", Value = 800 },
                            new Clue { Question = "This treaty ended World War I", Answer = "The Treaty of Versailles", Value = 1000 }
                        }
                    },
                    new Category
                    {
                        Name = "Geography",
                        Clues = new List<Clue>
                        {
                            new Clue { Question = "This is the longest river in the world", Answer = "The Nile", Value = 200 },
                            new Clue { Question = "This is the highest mountain in the world", Answer = "Mount Everest", Value = 400 },
                            new Clue { Question = "This is the largest ocean", Answer = "The Pacific Ocean", Value = 600 },
                            new Clue { Question = "This is the smallest continent", Answer = "Australia", Value = 800 },
                            new Clue { Question = "This is the largest desert in the world", Answer = "The Antarctic Desert", Value = 1000 }
                        }
                    }
                }
            }
        };
    }
}

public class GameTemplate
{
    public string Name { get; set; } = string.Empty;
    public List<Category> Categories { get; set; } = new();
}

