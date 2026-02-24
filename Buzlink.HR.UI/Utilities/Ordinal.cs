namespace Buzlink.HR.UI
{
    internal class Ordinal
    {
        public static string GetOrdinal(int Number)
        {

            // Accepts an integer, returns the ordinal suffix 
            // Handles special case three digit numbers ending
            // with 11, 12 or 13 - ie, 111th, 112th, 113th, 211th, et al
            if (System.Convert.ToString(Number).Length > 2)
            {
                int intEndNum = System.Convert.ToInt32(System.Convert.ToString(Number).Substring(System.Convert.ToString(Number).Length - 2, 2));
                if (intEndNum >= 11 & intEndNum <= 13)
                {
                    switch (intEndNum)
                    {
                        case 11:
                        case 12:
                        case 13:
                            {
                                return "th";
                            }
                    }
                }
            }

            if (Number >= 21)
            {
                // Handles 21st, 22nd, 23rd, et al
                switch (System.Convert.ToInt32(Number.ToString().Substring(Number.ToString().Length - 1, 1)))
                {
                    case 1:
                        {
                            return "st";
                        }

                    case 2:
                        {
                            return "nd";
                        }

                    case 3:
                        {
                            return "rd";
                        }

                    case 0:
                    case object _ when 4 <= System.Convert.ToInt32(Number.ToString().Substring(Number.ToString().Length - 1, 1)) && System.Convert.ToInt32(Number.ToString().Substring(Number.ToString().Length - 1, 1)) <= 9:
                        {
                            return "th";
                        }
                }
            }
            else
                // Handles 1st to 20th
                switch (Number)
                {
                    case 1:
                        {
                            return "st";
                        }

                    case 2:
                        {
                            return "nd";
                        }

                    case 3:
                        {
                            return "rd";
                        }

                    case object _ when 4 <= Number && Number <= 20:
                        {
                            return "th";
                        }
                }
            return "";
        }

    }
}
