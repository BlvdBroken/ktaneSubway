using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KModkit;
using System.Linq;
using System;
using System.Reflection;

public class subwayScript : MonoBehaviour {

    // Standard module variables

    public KMBombModule Module;
    public KMBombInfo Info;
    public KMAudio Audio;

    public KMSelectable[] arrowSelectables;
    public KMSelectable repeatSelectable, orderSelectable, ingredientSelectable;

    public MeshRenderer trayRenderer;
    public GameObject trayCoverObject;
    public Material trayMat, selectedTrayMat;
    public TextMesh ingredientText, buttonText;

    public AudioClip[] voiceLines;
    public AudioClip oatmeal;

    static int moduleIdCounter = 1;
    int moduleId;
    bool solved = false;
    static bool orderPlaying = false;

    private double Voltage() // shamelessly stolen from Access Codes, thanks GhostSalt
    {
        if (Info.QueryWidgets("volt", "").Count() != 0)
        {
            double TempVoltage = double.Parse(Info.QueryWidgets("volt", "")[0].Substring(12).Replace("\"}", ""));
            return TempVoltage;
            //return PublicVoltage;
        }
        return 0;
        //return -PublicVoltage;
    }

    // Normal variables

    int tipThreshold = 0;
    int changeValues = 0;
    int requiredChangeValues = 0;
    int remainingThreshold = 0;
    static readonly string[][] ingredients =
    {
        new string[6] { "WHITE", "MULTIGRAIN", "GLUTEN\nFREE", "WHOLE WHEAT", "CHEESE\nPIZZA", "PEPPERONI\nPIZZA" },
        new string[6] { "TUNA", "CHICKEN", "TURKEY", "HAM", "PASTRAMI", "MYSTERY\nMEAT" },
        new string[6] { "AMERICAN", "MOZZARELLA", "PROVOLONE", "SWISS", "CHEDDAR", "TOAST\nTHE BREAD" },
        new string[6] { "OLIVES", "LETTUCE", "PICKLES", "ONIONS", "TOMATOES", "JALAPENOS" },
        new string[6] { "KETCHUP", "MAYONNAISE", "RANCH", "SALT", "PEPPER", "VINEGAR" }
    };

    static readonly string[] sortTPList =
    {
        "white", "multigrain", "gluten", "whole", "cheese", "pepperoni", "tuna", "chicken", "turkey", "ham", "pastrami", "mystery", "american", "mozzarella", "provolone", "swiss", "cheddar", "toast", "olives", "lettuce", "pickles", "onions", "tomatoes", "jalapenos", "ketchup", "mayonnaise", "ranch", "salt", "pepper", "vinegar"
    };
    List<int>[] auto;

    bool orderActivated = false;
    int currentStage = 0;
    static readonly string[] stageNames = { "bread(s)", "meat(s)", "cheese(s)", "veggie(s)", "condiment(s)" };
    int ingredientPosition = 0;

    // Order related variables

    List<int>[] order = { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };
    List<int>[] sayorder = { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };
    //int ingredientCount = 0;
    bool melt = false;

    bool pizzaTime = false;
    bool asMuch = false;
    int asMuchPos = 0;
    int asMuchType = 0;
    int asMuchCounter = 0;
    //bool vegetlblbgbelabe = false;
    bool replaceTuna = false;
    bool changeCheese = false;

    int[] amounts;

    List<int>[] sandwichMade = { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };
    static readonly int[] changeTable =
    {
        3, 4, 1, 2, 2, // change
        0, 9, 4, 3, 2 // remove
    };
    /*int[] additions = { 0, 0, 0, 0, 0 };
    int[] changes = { 0, 0, 0, 0, 0 };
    int[] deletions = { 0, 0, 0, 0, 0 };*/

    int speaker = 2;
    /*static readonly float[][] audioLengths =
    {
        new float[] { .759f, .703f, .777f, .411f, .957f, 9.743f, .801f, 1.19f, 1.342f, .957f, 1.117f, 1.148f, .492f, .499f, .556f, 0f missing voice line here, .717f, 2.323f, 1.110f, 1.178f, 1.014f, .949f, .968f, .484f, .656f, .587f, .545f, .69f, .633f, .843f, .377f, .56f, .549f, .564f, .434f, .595f },
        new float[] { 2.474f, },
    };*/
    static readonly string[][] audioIngredients =
    {
        new string[] { "white bread", "multigrain bread", "gluten free bread", "whole wheat bread", "cheese pizza", "pepperoni pizza" },
        new string[] { "tuna", "chicken", "turkey", "ham", "pastrami", "mystery meat" },
        new string[] { "american cheese", "mozzarella cheese", "provolone cheese", "swiss cheese", "cheddar cheese", "toast the bread" },
        new string[] { "olives", "lettuce", "pickles", "onions", "tomatoes", "jalapenos" },
        new string[] { "ketchup", "mayonnaise", "ranch", "salt", "pepper", "vinegar" }
    };
    
    private void Awake()
    {
        moduleId = moduleIdCounter++;
        for (int i = 0; i < 2; i++)
        {
            int j = i;
            arrowSelectables[i].OnInteract += delegate ()
            {
                if (!solved)
                {
                    MoveIngredient(j * 2 - 1);
                    arrowSelectables[j].AddInteractionPunch();
                    Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, arrowSelectables[j].transform);
                }
                return false;
            };
        }
        repeatSelectable.OnInteract += delegate ()
        {
            if (!solved && !orderPlaying)
            {
                StartCoroutine(SayOrder());
                repeatSelectable.AddInteractionPunch();
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, repeatSelectable.transform);
            }
            return false;
        };
        orderSelectable.OnInteract += delegate ()
        {
            if (!solved && !orderPlaying)
            {
                StartCoroutine(AdvanceStage());
                orderSelectable.AddInteractionPunch();
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, orderSelectable.transform);
            }
            return false;
        };
        ingredientSelectable.OnInteract += delegate ()
        {
            if (!solved)
            {
                if (orderActivated && !sandwichMade[currentStage].Contains(ingredientPosition))
                {
                    sandwichMade[currentStage].Add(ingredientPosition);
                    MoveIngredient(0);
                    ingredientSelectable.AddInteractionPunch();
                    Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, ingredientSelectable.transform);
                }
                if (asMuch && (asMuchType == currentStage) && (order[currentStage][asMuchPos] == ingredientPosition))
                {
                    StartCoroutine(AsMuchHandler());
                }
            }
            return false;
        };
        /*GetComponent<KMGameInfo>().OnStateChange += state => {
            if (state == KMGameInfo.State.PostGame)
            {
                if (GetMissionID() == "mod_blvd_oatmeal")
                {
                    Audio.PlaySoundAtTransform("oatmeal", transform);
                    DebugMsg("Oh, oh, the missouri");
                }
            }
        };*/
    }

    /*void StateChange () {
      // :))) (plays sound when blowing up or solving on oatmeal bomb)
        
    }*/

    private void Start()
    {
        pizzaTime = false;
        asMuch = false;
        // vegetlblbgbelabe = false;
        speaker = UnityEngine.Random.Range(0, 3);
        Debug.LogFormat("Speaker is {0}", speaker);

        tipThreshold = new int[3] { Info.GetBatteryHolderCount(), Info.GetIndicators().Count(), Info.GetPortPlateCount() }.Max() * 3;
        tipThreshold += (int)Voltage();
        DebugMsg("The tip threshold is " + tipThreshold + ".");

        DebugMsg("The order is...");
        if (UnityEngine.Random.Range(0, 20) == 0)
        {
            pizzaTime = true;
            order[0].Add(UnityEngine.Random.Range(4, 6));
            DebugMsg(ingredients[0][order[0][0]].Replace('\n', ' ') + ".");
        }
        else
        {
            if (UnityEngine.Random.Range(0, 20) == 0)
                asMuch = true;
            /*else if (UnityEngine.Random.Range(0, 25) == 0 && tipThreshold != 0)
            {
                vegetlblbgbelabe = true;
                int[] shuffledArray = { 0, 1, 2, 3, 4, 5 };
                int veggieCount = UnityEngine.Random.Range(5, 7);
                shuffledArray.Shuffle(); 
                for (int j = 0; j < veggieCount; j++)
                    order[3].Add(shuffledArray[j]);
                requiredChangeValues += 3 * (veggieCount - 4);
            }*/

            // add bread (if there's not enough tip threshold, don't add whole wheat bread)
            if (tipThreshold < 3)
            {
                order[0].Add(UnityEngine.Random.Range(0, 3));
            }
            else
            {
                order[0].Add(UnityEngine.Random.Range(0, 4));
            }

            amounts = new int[] { 1, UnityEngine.Random.Range(1, 4), UnityEngine.Random.Range(1, 3), UnityEngine.Random.Range(1, 6), UnityEngine.Random.Range(1, 5) };

            int[] shuffledMeaties = { 0, 1, 2, 3, 4, 5 };
            int[] shuffledCheeses = { 0, 1, 2, 3, 4 }; // dont want to add toast randomly, that's set later
            int[] shuffledVeggies = { 0, 1, 2, 3, 4, 5 };
            int[] shuffledCondoms = { 0, 1, 2, 3, 4, 5 };
            shuffledMeaties.Shuffle();
            shuffledCheeses.Shuffle();
            shuffledVeggies.Shuffle();
            shuffledCondoms.Shuffle();
            
            int[][] Shuffles = { new int[] {}, shuffledMeaties, shuffledCheeses, shuffledVeggies, shuffledCondoms };

            for (int i = 1; i < 5; i++)
            {
                for (int j = 0; j < amounts[i - 1]; j++)
                {
                    if (order[1].Contains(0) && (i == 4) && (Shuffles[i][j] == 1)) // tuna mayonnaise contradiction prevention
                    {
                        order[i].Add(Shuffles[i][5]);
                    }
                    else
                    {
                        order[i].Add(Shuffles[i][j]);
                    }
                }
            }

            /*ingredientCount = UnityEngine.Random.Range(6, 10);
            if (vegetlblbgbelabe)
                ingredientCount = UnityEngine.Random.Range(2, 5);

            for (int i = 0; i < ingredientCount; i++)
            {
                int placeholder = UnityEngine.Random.Range(1, 5);
                if (order[1].Count == 0)
                    placeholder = 1;
                else if (order[2].Count == 0)
                    placeholder = 2;
                else if (order[3].Count == 0)
                    placeholder = 3;
                else if (order[4].Count == 0)
                    placeholder = 4;
                while (order[placeholder].Count >= 4 || (order[2].Count == 1 && placeholder == 2))
                    placeholder = UnityEngine.Random.Range(1, 5);

                int placeholderIngredient = UnityEngine.Random.Range(0, 6);

                switch (placeholder)
                {
                    case 1: // meats
                        while (order[placeholder].Contains(placeholderIngredient) || (order[4].Contains(1) && placeholderIngredient == 0))
                            placeholderIngredient = UnityEngine.Random.Range(0, 6);
                            break;
                    case 2: // cheese
                        while (order[placeholder].Contains(placeholderIngredient))
                            placeholderIngredient = UnityEngine.Random.Range(0, 5);
                        break;
                    case 3: // veggies
                        while (order[placeholder].Contains(placeholderIngredient))
                            placeholderIngredient = UnityEngine.Random.Range(0, 6);
                        break;
                    case 4: // condiments
                        while (order[placeholder].Contains(placeholderIngredient) || (requiredChangeValues + 1 <= tipThreshold && placeholderIngredient == 0) || (order[1].Contains(0) && placeholderIngredient == 1))
                            placeholderIngredient = UnityEngine.Random.Range(0, 6);
                        if (placeholderIngredient == 0)
                            requiredChangeValues += 1;
                        break;
                }


                order[placeholder].Add(placeholderIngredient);


            }*/

            for (int i = 0; i < 5; i++)
                foreach (var ingredient in order[i])
                    DebugMsg(ingredients[i][ingredient]);
        
        }

        sayorder = order; // new order variable so tuna doesnt mess up
        
        if (!pizzaTime)
        {
            if (UnityEngine.Random.Range(0, 10) == 0)
            {
                order[2].Add(5);
                melt = true;
                DebugMsg("Also, the customer asked for a melt! You should TOAST THE BREAD. Or not.");
            }
            else if (UnityEngine.Random.Range(0, 10) == 0)
            {
                order[2].Add(5);
                DebugMsg("Also, the customer asked you to TOAST THE BREAD.");
            }
        }
        if (asMuch) {
            asMuchType = UnityEngine.Random.Range(1, 5);
            asMuchPos = UnityEngine.Random.Range(0, amounts[asMuchType - 1]);
            Debug.LogFormat("{0} is asMuchType {1} is asMuchPos", asMuchType, asMuchPos);
            while ((asMuchType == 3) && (order[asMuchType][asMuchPos] == 5))
            {
                asMuchType = UnityEngine.Random.Range(1, 5);
                asMuchPos = UnityEngine.Random.Range(0, amounts[asMuchType - 1]);
            }
            Debug.LogFormat("{0} is asMuchType {1} is asMuchPos", asMuchType, asMuchPos);
            /* while (order[asMuchType][asMuchPos] == 0 && (asMuchType == 1 || asMuchType == 4))
            {
                asMuchType = UnityEngine.Random.Range(1, 5);
                asMuchPos = UnityEngine.Random.Range(0, order[asMuchType].Count);
            } */
            // DebugMsg(asMuchType + "type and " + asMuchPos + "pos");
            DebugMsg("Also, the customer asked for as much " + ingredients[asMuchType][order[asMuchType][asMuchPos]] + " that will get you fired! Spam that button when you get to that category.");
        }
        else if (pizzaTime)
        {
            DebugMsg("The customer asked for a pizza! Get that customer a piece-a' pizza.");
        }
        else
        {
            if (order[0].Contains(3))
            {
                DebugMsg("Also, the customer asked for whole wheat bread! Change it.");
                requiredChangeValues += 3;
            }
            if (order[4].Contains(0))
            {
                DebugMsg("Also, the customer asked for ketchup! Either remove it or change it.");
                requiredChangeValues += 2;
            }
            if (order[1].Contains(0))
            {
                replaceTuna = true;
                DebugMsg("Also, the customer asked for tuna! Make it mayonnaise.");
            }
            // if (order[3].Count > 6) { DebugMsg("Also, the customer asked for more than 6 veggies! Get rid of them until there's 6."); }
            if (requiredChangeValues < tipThreshold)
            {
                changeCheese = true;
                DebugMsg("Also, there's still tip threshold remaining after all required changes! Change the cheese.");
                requiredChangeValues += 1;
            }
        }

        // order[1].Remove(0);

        auto = FindPossibleAnswer();
    }

    void MoveIngredient(int dir)
    {
        if (orderActivated)
        {
            ingredientPosition = (ingredientPosition + dir) % 6;
            if (ingredientPosition < 0)
                ingredientPosition += 6;
            ingredientText.text = ingredients[currentStage][ingredientPosition];

            if (sandwichMade[currentStage].Contains(ingredientPosition))
                trayRenderer.material = selectedTrayMat;
            else
                trayRenderer.material = trayMat;
        }
    }

    IEnumerator SayOrder()
    {
        orderPlaying = true;
        yield return new WaitForSeconds(1f);
        Audio.PlaySoundAtTransform("greeting" + speaker, Module.transform);
        yield return new WaitForSeconds(voiceLines[speaker * 39].length);
        if (pizzaTime)
        {
            Audio.PlaySoundAtTransform(audioIngredients[0][sayorder[0][0]] + speaker, Module.transform);
            yield return new WaitForSeconds(voiceLines[speaker * 39 + 6 + sayorder[0].First()].length);
        }
        else
        {
            if (melt)
            {
                Audio.PlaySoundAtTransform("melt with" + speaker, Module.transform);
                yield return new WaitForSeconds(voiceLines[speaker * 39 + 1].length);
            }
            else
            {
                Audio.PlaySoundAtTransform("sandwich with" + speaker, Module.transform);
                yield return new WaitForSeconds(voiceLines[speaker * 39 + 2].length);
            }

            for (int i = 0; i < 5; i++)
            {
                foreach (var ingredient in sayorder[i])
                {
                    //DebugMsg("order[i] is " + order[i].Join());
                    
                    if (i == 4 && ingredient == sayorder[4].Last()) // say "and" after all ingredients except the last
                    {
                        Audio.PlaySoundAtTransform("and" + speaker, Module.transform);
                        yield return new WaitForSeconds(voiceLines[speaker * 39 + 3].length);
                    }

                    if (asMuch && (asMuchType == i) && (sayorder[i][asMuchPos] == ingredient))
                    {
                        Audio.PlaySoundAtTransform("as much" + speaker, Module.transform);
                        yield return new WaitForSeconds(voiceLines[(speaker + 1) * 39 - 3].length);
                    }

                    if (!(i == 2 && ingredient == 5)) // toast the bread goes last
                    {
                        // DebugMsg(i + " " + ingredient);
                        Audio.PlaySoundAtTransform(audioIngredients[i][ingredient] + speaker, Module.transform);
                        yield return new WaitForSeconds(voiceLines[speaker * 39 + 6 * (i + 1) + ingredient].length);
                    }

                    if (asMuch && (asMuchType == i) && (sayorder[i][asMuchPos] == ingredient))
                    {
                        Audio.PlaySoundAtTransform("fired" + speaker, Module.transform);
                        yield return new WaitForSeconds(voiceLines[(speaker + 1) * 39 - 2].length);
                    }

                }
            }

            if (sayorder[2].Contains(5))
                Audio.PlaySoundAtTransform(audioIngredients[2][5] + speaker, Module.transform);
        }
        
        orderPlaying = false;
        orderActivated = true;
        buttonText.text = "NEXT";
        DebugMsg("Order button pressed. Order up!");
        trayCoverObject.SetActive(false);
        MoveIngredient(0);
    }

    // when orderSelectable is pressed
    IEnumerator AdvanceStage()
    {
        if (!orderActivated)
        {
            StartCoroutine(SayOrder());
        }

        else
        {
            DebugMsg("Completed the " + stageNames[currentStage] + " stage.");

            if(sandwichMade[0].Contains(4) || sandwichMade[0].Contains(5))
            {
                currentStage = 5;
            }
            else 
            {
                currentStage++;
            }

            if (currentStage == 5)
            {
                int[] additions = { 0, 0, 0, 0, 0 };
                int[] changes = { 0, 0, 0, 0, 0 };
                int[] deletions = { 0, 0, 0, 0, 0 };
                trayRenderer.material = trayMat;
                ingredientText.text = "";
                orderActivated = false;
                DebugMsg("Sandwich prepared. Your sandwich consisted of:");
                for (int i = 0; i < 5; i++)
                    foreach (var ingredient in sandwichMade[i])
                        DebugMsg(ingredients[i][ingredient].Replace('\n', ' '));

                // Check if the sandwich is correct
                bool sandwichCorrect = true;
                List<string> reasonsWhy = new List<string>();

                if (pizzaTime)
                {
                    // Debug.LogFormat("order0 is {0} and sand0 is {1}", order[0][0], sandwichMade[0][0]);
                    if ((sandwichMade[0].Count() > 1) || !(sandwichMade[0].Contains(4) || sandwichMade[0].Contains(5)) || (order[0][0] == sandwichMade[0][0]))
                    {
                        sandwichCorrect = false;
                        reasonsWhy.Add("... you did not give the customer (the wrong) pizza!");
                    }
                }
                else
                {
                    if (sandwichMade[0].Count == 0)
                    { 
                        sandwichCorrect = false;
                        yield return ATONEFORYOURSINS();
                    }
                    if (sandwichMade[3].Count == 0)
                    {
                        sandwichCorrect = false;
                        reasonsWhy.Add("... you got rid of the last vegetable on the sandwich!");
                    }
                    if (asMuch) 
                    {
                        sandwichCorrect = false;
                        reasonsWhy.Add("... you didn't spam the " + ingredients[asMuchType][order[asMuchType][asMuchPos]] + " button!");
                    }
                    if (sandwichMade[0].Contains(3))
                    {
                        sandwichCorrect = false;
                        reasonsWhy.Add("... you gave the customer whole wheat bread!");
                    }
                    if (sandwichMade[4].Contains(0) && order[4].Contains(0))
                    {
                        sandwichCorrect = false;
                        reasonsWhy.Add("... you gave the customer ketchup when they asked for it!");
                    }
                    if (order[1].Contains(0) && (sandwichMade[1].Contains(0)))
                    {
                        sandwichCorrect = false;
                        reasonsWhy.Add("... you didn't replace the tuna!");
                    } // this might be weird. you can't change/remove the substituted mayo
                    // if (sandwichMade[3].Count > 6 && order[3].Count > 6) { sandwichCorrect = false; reasonsWhy.Add("... you didn't get rid of veggies until there were less than 6 on the sandwich!"); }
                    // if (changeCheese && order[2].Where(x => x != 5) == sandwichMade[2].Where(x => x != 5)) { sandwichCorrect = false; reasonsWhy.Add("... you didn't change the cheese when you had the chance!"); }
                    /*if (order[0].Count < sandwichMade[0].Count || order[1].Count < sandwichMade[1].Count || order[2].Count < sandwichMade[2].Count || order[3].Count < sandwichMade[3].Count || (replaceTuna && order[4].Count < sandwichMade[4].Count - 1) || (!replaceTuna && order[4].Count < sandwichMade[4].Count))
                    {
                        sandwichCorrect = false;
                        reasonsWhy.Add("... you added an extra ingredient without removing an equivalent one!");
                    }*/

                    changeValues = 0;
                    if (replaceTuna) // replace tuna with mayo for this part
                    {
                        order[1].Remove(0);
                        order[4].Add(1);
                    }

                    for (int i = 0; i < 5; i++)
                    {
                        if (order[i].Count < sandwichMade[i].Count)
                        {
                            DebugMsg("You added extra " + stageNames[i] + ".");
                            sandwichCorrect = false;
                            reasonsWhy.Add("... you added an item when you weren't supposed to!");
                        }
                        else
                        {
                            foreach (var ingredient in order[i])
                            {
                                //Debug.LogFormat("{0} {1}", ingredient, sandwichMade[i].Contains(ingredient));
                                if (!(sandwichMade[i].Contains(ingredient)))
                                {
                                    if (sandwichMade[i].Count + deletions[i] - additions[i] > order[i].Count)
                                    {
                                        additions[i]++;
                                    }
                                    else if (sandwichMade[i].Count + deletions[i] - additions[i] == order[i].Count)
                                    {
                                        changes[i]++;
                                    }
                                    else
                                    {
                                        deletions[i]++;
                                    }
                                }
                            }

                            DebugMsg("You changed " + changes[i] + " " + stageNames[i] + ".");
                            DebugMsg("You deleted " + deletions[i] + " " + stageNames[i] + ".");

                            changeValues += changeTable[i] * changes[i];
                            changeValues += changeTable[i + 5] * deletions[i];
                        }
                    }

                    /*for (int i = 0; i < 5; i++)
                    {
                        Debug.LogFormat("{0} Additions / {1}", i, additions[i]);
                        Debug.LogFormat("{0} Changes / {1}", i, changes[i]);
                        Debug.LogFormat("{0} Deletions / {1}", i, deletions[i]);
                    }*/

                    if (changeCheese && changes[2] == 0)
                    {
                        sandwichCorrect = false;
                        reasonsWhy.Add("... you didn't change the cheese when you had the chance!");
                    }

                    DebugMsg("The total value of your changes is " + changeValues + ".");

                    Debug.LogFormat("changeValues is {0} and requiredChangeValues is {1}", changeValues, requiredChangeValues);

                    if ((changeValues > tipThreshold + 9) ? replaceTuna : (changeValues > tipThreshold))
                    {
                        sandwichCorrect = false;
                        reasonsWhy.Add("... you went over the tip threshold!");
                    }
                    else if (changeValues < tipThreshold)
                    {
                        if ((remainingThreshold == tipThreshold - changeValues + 9) ? replaceTuna : (remainingThreshold == tipThreshold - changeValues))
                        {
                            DebugMsg("Your sandwich is valid and is close enough to the tip threshold!");
                            //sandwichCorrect = true;
                        }   
                        else
                        {
                            reasonsWhy.Add("... your sandwich isn't close enough to the tip threshold!");
                            sandwichCorrect = false;
                        }
                    }
                    else
                    {
                        DebugMsg("Your sandwich is valid and is close enough to the tip threshold!");
                        //sandwichCorrect = true;
                    }
                }

                if (sandwichCorrect)
                {
                    solved = true;
                    Audio.PlaySoundAtTransform("success" + speaker, Module.transform);
                    yield return new WaitForSeconds(voiceLines[speaker * 39 + 4].length);
                    Module.HandlePass();
                }
                else
                {
                    if (replaceTuna) // put the tuna back
                    {
                        order[4].Remove(1);
                        order[1].Add(0);
                    }
                    DebugMsg("Your sandwhich was not valid because:");
                    foreach (var reason in reasonsWhy)
                    {
                        DebugMsg(reason);
                    }
                    Audio.PlaySoundAtTransform("failure" + speaker, Module.transform);
                    yield return new WaitForSeconds(voiceLines[speaker * 39 + 5].length);
                    Module.HandleStrike();
                }

                // cleanup after a strike
                currentStage = 0;
                for (int i = 0; i < 5; i++)
                    sandwichMade[i].Clear();
                orderActivated = false;
                trayCoverObject.SetActive(true);
                buttonText.text = "ORDER";
            }
        }

        //ingredientPosition = UnityEngine.Random.Range(0, 6);
        // was nice to start somewhere random, but breaks the twitch play :(
        ingredientPosition = 0;
        MoveIngredient(0);
    }

    // Finds a possible solution for the logging and autosolver
    List<int>[] FindPossibleAnswer()
    {
        if (replaceTuna)
        {
            order[4].Add(1);
            order[1].Remove(0);
        } // begone tuna
        DebugMsg("Calculated solution:");
        remainingThreshold = tipThreshold - requiredChangeValues;
        int[] tempModifications = { 0, 0, 0, 0, 0 };
        int[] tempChanges = { 0, 0, 0, 0, 0 };
        int[] tempRemovals = { 0, 0, 0, 0, 0 };
        // thanks Timwi, unlinks the reference types
        var possibleOrder = order.Select(list => list.ToList()).ToArray();
        bool catsup = false;

        tempModifications[3]++; // cannot remove the last vegetable
        if (order[0].Contains(3))
        {
            tempModifications[0]++;
            tempChanges[0]++;
        } // always change whole wheat
        if (order[4].Contains(0))
        {
            tempModifications[4]++;
            possibleOrder[4].Remove(0);
            catsup = true;
        } // always remove ketchup
        if (replaceTuna)
        {
            DebugMsg("Replace the tuna with mayonnaise...");
        } // replace tuna with mayo, can't re-change the mayo
        // if (order[3].Count > 6) { tempModifications[3] += order[3].Count - 6; } // remove veggies until there are 6
        if (changeCheese)
        {
            tempModifications[2]++;
            tempChanges[2]++;
        } // always change cheese

        // going from most expensive to least expensive change should always give best answer
        while (remainingThreshold >= 9 && order[1].Count() - tempModifications[1] > 0)
        {
            tempModifications[1]++;
            tempRemovals[1]++;
            remainingThreshold -= 9;
        } // remove meat
        while (remainingThreshold >= 4 && order[2].Count() - tempModifications[2] > 0)
        {
            tempModifications[2]++;
            tempRemovals[2]++;
            remainingThreshold -= 4;
        } // remove cheese
        while (remainingThreshold >= 4 && order[1].Count() - tempModifications[1] > 0)
        {
            tempModifications[1]++;
            tempChanges[1]++;
            remainingThreshold -= 4;
        } // change meat
        while (remainingThreshold >= 3 && order[0].Count() - tempModifications[0] > 0)
        {
            tempModifications[0]++;
            tempChanges[0]++;
            remainingThreshold -= 3;
        } // change bread
        while (remainingThreshold >= 3 && order[3].Count() - tempModifications[3] > 0)
        {
            tempModifications[3]++;
            tempRemovals[3]++;
            remainingThreshold -= 3;
        } // remove veggie
        while (remainingThreshold >= 2 && order[3].Count() - tempModifications[3] > 0)
        {
            tempModifications[3]++;
            tempChanges[3]++;
            remainingThreshold -= 2;
        } // change veggie
        while (remainingThreshold >= 2 && order[4].Count() - tempModifications[4] > 0)
        {
            tempModifications[4]++;
            tempRemovals[4]++;
            remainingThreshold -= 2;
        } // remove condiment
        while (remainingThreshold >= 1 && order[2].Count() - tempModifications[2] > 0)
        {
            tempModifications[2]++;
            tempChanges[2]++;
            remainingThreshold -= 1;
        } // change cheese
        /*for (int i = 0; i < 5; i++)
            foreach (var ingrede in possibleOrder[i])
                Debug.LogFormat("{0} is i and {1} is ingrede", i, ingrede);*/
        int tempVal;
        for (int i = 0; i < 5; i++)
        {
            if (tempChanges[i] != 0)
            {
                DebugMsg("Change " + tempChanges[i] + " " + stageNames[i] + "...");
                // for changes, adds 1 to ingredient value until that value not present
                for (int j = 0; j < tempChanges[i]; j++)
                {
                    tempVal = (i > 0) ? ((possibleOrder[i][j] + 1) % 6) : ((possibleOrder[i][j] + 1) % 3);
                    while (possibleOrder[i].Contains(tempVal))
                        tempVal = (i > 0) ? ((tempVal + 1) % 6) : ((tempVal + 1) % 3);
                    possibleOrder[i][j] = tempVal;
                }
            }
            if (tempRemovals[i] != 0)
            {
                DebugMsg("Remove " + tempRemovals[i] + " " + stageNames[i] + "...");
                // for removals, deletes at end instead of first index so doesn't conflict with previous changes
                for (int j = 0; j < tempRemovals[i]; j++)
                    possibleOrder[i].RemoveAt(possibleOrder[i].Count - 1);
            }
        }
        if (catsup)
        {
            DebugMsg("Remove or change the ketchup...");
        }
        /*for (int i = 0; i < 5; i++)
            foreach (var ingrede in possibleOrder[i])
                Debug.LogFormat("{0} is i and {1} is ingrede", i, ingrede);*/

        if (replaceTuna)
        {
            order[4].Remove(1);
            order[1].Add(0);
        }//return to tuna
        return possibleOrder;
    }
    
    // Runs when you don't submit bread. Idiot.
    IEnumerator ATONEFORYOURSINS()
    {
        DebugMsg("That was incorrect, because...");
        DebugMsg("... you, uh... you... what the fuck?");
        if (sandwichMade[1].Count > 0 || sandwichMade[2].Count() > 0 || sandwichMade[3].Count() > 0 || sandwichMade[4].Count() > 0)
        {
            DebugMsg("You didn't put any bread on it! It's a sandwich. How could you forget the bread?");
            DebugMsg("The customer is rendered speechless as you hand them the \"sandwich\" you just created.");
            DebugMsg("In your hands is a sopping heap of various sandwich ingredients. Without the necessary bread, it drips down your sleeves and coats the floor.");
            DebugMsg("Your co-worker stares at you in awe. Or fear. Or both. They knew this job was mind-numbing, but they didn't know it was possible for a human being to be this far gone.");
            if (sandwichMade[2].Contains(4))
            {
                DebugMsg("You even toasted it. You toasted it, and you didn't put any bread. You put a pile of sandwich ingredients in the oven and had, like, a full minute to think about what you'd done.");
                DebugMsg("The toaster oven is now coated in a puddle of melted cheese and liquefied meat.");
                DebugMsg("The skin of your hands starts to slough off from the heat. You would be in an extreme amount of pain if you weren't so absent mentally.");
            }
            DebugMsg("After what feels like an unbearable amount of time, your manager comes out to investigate the disturbance. You get fired on the spot.");
        }

        else
        {
            DebugMsg("You didn't even make a sandwich...");
            DebugMsg("You get fired on the spot.");
        }

        DebugMsg("You had one simple job, and you failed. Now, you must die.");
        while (!solved)
        {
            Module.HandleStrike();
            DebugMsg("Strike!");
            yield return new WaitForSeconds(.1f);
        }
    }

    // Special solving method for when asMuch occurs
    IEnumerator AsMuchHandler()
    {
        asMuchCounter++;
        // DebugMsg(asMuchCounter.ToString());
        if (asMuchCounter >= 30)
        {
            solved = true;
            Audio.PlaySoundAtTransform("as success" + speaker, Module.transform);
            yield return new WaitForSeconds(voiceLines[(speaker + 1) * 39 - 1].length);
            Module.HandlePass();
        }
    }

    void DebugMsg(string msg)
    {
        Debug.LogFormat("[Subway #{0}] {1}", moduleId, msg.Replace('\n', ' '));
    }

    void OnDestroy()
    {
        // prevents softlocking when bomb explodes mid speech
        orderPlaying = false;
    }

    private string GetMissionID () {
        try {
            Component gameplayState = GameObject.Find("GameplayState(Clone)").GetComponent("GameplayState");
            Type type = gameplayState.GetType();
            FieldInfo fieldMission = type.GetField("MissionToLoad", BindingFlags.Public | BindingFlags.Static);
            return fieldMission.GetValue(gameplayState).ToString();
        }

        catch (NullReferenceException) {
             return "undefined";
        }
    }

    // Teeper code
    #pragma warning disable 414
    	private readonly string TwitchHelpMessage = @"Use !{0} order to take the customer's order, and !{0} repeat to have them repeat it. Use !{0} prepare [ingredient] [ingredient] etc. to submit your masterpiece to the customer. Use !{0} keep adding [ingredient] to give the customer way too much of one item. Make sure to use only the first word of each ingredient or modification. Use !{0} ingredients to see a list of valid ingredients.";
    #pragma warning restore 414

	IEnumerator ProcessTwitchCommand(string command)
	{
		command = command.Trim().ToLowerInvariant();
        int ProcessTwitchCommand = 0;
		if (command == "order" || command == "repeat")
		{
			// Presses repeat since orderSelectable turns into next button
			repeatSelectable.OnInteract();
        } else if (command == "ingredients")
        {
            yield return "sendtochat Acceptable ingredient names are: " + String.Join(", ", sortTPList);
		} else if (command.Split()[0] == "prepare")
		{
			string[] prepared = command.Split().Skip(1).Where(s => !(string.IsNullOrEmpty(s))).ToArray();
            int nextCounter = 0;
            foreach (var submitted in prepared)
            {
                if (!(sortTPList.Contains(submitted)))
                    yield return "sendtochat Sorry, I'm not quite sure what a " + submitted + " is.";
            }
            foreach (String item in prepared)
            {
                DebugMsg(item);
            }
            foreach (String bread in sortTPList.Take(5))
            {
                DebugMsg(bread);
            }
            if (prepared.Length == 0)
            {
                yield return "sendtochat You didn't even make a sandwich... You get fired on the spot.";
                yield return "detonate";
            }
            else if (!(prepared.Intersect(sortTPList.Take(5)).Any()))
            {
                yield return "sendtochat You didn't put any bread on it! It's a sandwich. How could you forget the bread? The customer is rendered speechless as you hand them the \"sandwich\" you just created. In your hands is a sopping heap of various sandwich ingredients. Without the necessary bread, it drips down your sleeves and coats the floor. Your co-worker stares at you in awe. Or fear. Or both. They knew this job was mind-numbing, but they didn't know it was possible for a human being to be this far gone.";
                yield return new WaitForSeconds(1f);
                if (prepared.Contains("toast"))
                {
                    yield return "sendtochat You even toasted it. You toasted it, and you didn't put any bread. You put a pile of sandwich ingredients in the oven and had, like, a full minute to think about what you'd done. The toaster oven is now coated in a puddle of melted cheese and liquefied meat. The skin of your hands starts to slough off from the heat. You would be in an extreme amount of pain if you weren't so absent mentally.";
                    yield return new WaitForSeconds(1f);
                }
                yield return "sendtochat After what feels like an unbearable amount of time, your manager comes out to investigate the disturbance. You get fired on the spot.";
                yield return "detonate";
            }
            else
            {
                orderActivated = true;
                buttonText.text = "NEXT";
                DebugMsg("Order button pressed. Order up!");
                trayCoverObject.SetActive(false);
                MoveIngredient(0);
                foreach (var item in sortTPList)
                {
                    if (prepared.Contains(item))
                        ingredientSelectable.OnInteract();
                    arrowSelectables[1].OnInteract();
                    nextCounter += 1;
                    if (nextCounter % 6 == 0)
                        orderSelectable.OnInteract();
                }
            }
		} else if ((command.Split()[0] == "keep") && (command.Split().Length == 3))
        {
            string keepCoding = command.Split()[2];
            if (!(sortTPList.Contains(keepCoding)))
                yield return "sendtochat Sorry, I'm not quite sure what a " + keepCoding + " is.";
            orderActivated = true;
            buttonText.text = "NEXT";
            DebugMsg("Order button pressed. Order up!");
            trayCoverObject.SetActive(false);
            MoveIngredient(0);
            int loquation = Array.IndexOf(sortTPList, keepCoding);
            for (int i = 0; i < (loquation / 6); i++)
                orderSelectable.OnInteract();
            for (int i = 0; i < (loquation % 6); i++)
                arrowSelectables[1].OnInteract();
            for (int i = 0; i < 30; i++)
                ingredientSelectable.OnInteract();
        } else
        {
            yield return "sendtochat Sorry, I'm not paid enough to pretend to understand what you said.";
        }
		yield return null;
	}

    /*
    // Since I cant do += for KMSelectable[]
    KMSelectable[] AddUp(KMSelectable[] inArr, KMSelectable press)
    {
        inArr[pressno] = press;
        pressno++;
        return inArr;
    }*/

	// Runs when !solve is used
	IEnumerator TwitchHandleForcedSolve()
	{
        orderActivated = true;
        buttonText.text = "NEXT";
        DebugMsg("Operation: eXish");
        trayCoverObject.SetActive(false);
        MoveIngredient(0);

        if (asMuch)
        {
            for (int i = 0; i < asMuchType; i++)
                orderSelectable.OnInteract();
            for (int j = 0; j < order[asMuchType][asMuchPos]; j++)
                arrowSelectables[1].OnInteract();
            for (int k = 0; k < 30; k++)
                ingredientSelectable.OnInteract();
            trayCoverObject.SetActive(true);
        }
        else if (pizzaTime)
        {
            Debug.LogFormat("{0}", order[0][0]);
            for (int i = 0; i < (9 - order[0][0]); i++)
                arrowSelectables[1].OnInteract();
            ingredientSelectable.OnInteract();
            orderSelectable.OnInteract();
        }
        else
        {
		    for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    if (auto[i].Contains(j))
                    {
                        ingredientSelectable.OnInteract();
                    }
                    arrowSelectables[1].OnInteract();
                }
                orderSelectable.OnInteract();
            }
        }

        yield return null;
	}

}