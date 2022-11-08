﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KModkit;
using System.Linq;

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

    static int moduleIdCounter = 1;
    int moduleId;
    bool solved = false;
    private bool orderPlaying = false;

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
    static readonly string[][] ingredients =
    {
        new string[6] { "WHITE", "MULTIGRAIN", "GLUTEN\nFREE", "WHOLE WHEAT", "CHEESE\nPIZZA", "PEPPERONI\nPIZZA" },
        new string[6] { "TUNA", "CHICKEN", "TURKEY", "HAM", "PASTRAMI", "MYSTERY\nMEAT" },
        new string[6] { "AMERICAN", "MOZZARELLA", "PROVOLONE", "SWISS", "CHEDDAR", "TOAST\nTHE BREAD" },
        new string[6] { "OLIVES", "LETTUCE", "PICKLES", "ONIONS", "TOMATOES", "JALAPENOS" },
        new string[6] { "KETCHUP", "MAYONNAISE", "RANCH", "SALT", "PEPPER", "VINEGAR" }
    };

    bool orderActivated = false;
    int currentStage = 0;
    static readonly string[] stageNames = { "bread(s)", "meat(s)", "cheese(s)", "veggie(s)", "condiment(s)" };
    int ingredientPosition = 0;

    // Order related variables

    List<int>[] order = { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };
    List<int>[] sayorder = { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };
    int ingredientCount = 0;
    bool melt = false;

    bool pizzaTime = false;
    bool asMuch = false;
    int asMuchPos = 0;
    int asMuchType = 0;
    int asMuchCounter = 0;
    bool vegetlblbgbelabe = false;
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
            arrowSelectables[i].OnInteract += delegate () { if (!solved) { MoveIngredient(j * 2 - 1); arrowSelectables[j].AddInteractionPunch(); Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, arrowSelectables[j].transform); } return false; };
        }
        repeatSelectable.OnInteract += delegate () { if (!solved && !orderPlaying) { StartCoroutine(SayOrder()); repeatSelectable.AddInteractionPunch(); Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, repeatSelectable.transform); } return false; };
        orderSelectable.OnInteract += delegate () { if (!solved && !orderPlaying) { StartCoroutine(AdvanceStage()); orderSelectable.AddInteractionPunch(); Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, orderSelectable.transform); } return false; };
        ingredientSelectable.OnInteract += delegate () { if (!solved) {
                if (orderActivated && !sandwichMade[currentStage].Contains(ingredientPosition)) { sandwichMade[currentStage].Add(ingredientPosition); MoveIngredient(0);
                ingredientSelectable.AddInteractionPunch();
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, ingredientSelectable.transform); }
                if (asMuch && (asMuchType == currentStage) && (order[currentStage][asMuchPos] == ingredientPosition)) { StartCoroutine(AsMuchHandler()); } }
                return false;
        };
    }

    private void Start()
    {
        pizzaTime = false;
        asMuch = false;
        // vegetlblbgbelabe = false;
        speaker = Random.Range(0, 3);

        tipThreshold = new int[3] { Info.GetBatteryHolderCount(), Info.GetIndicators().Count(), Info.GetPortPlateCount() }.Max() * 3;
        tipThreshold += (int)Voltage();
        DebugMsg("The tip threshold is " + tipThreshold + ".");

        DebugMsg("The order is...");
        if (Random.Range(0, 20) == 0)
        {
            pizzaTime = true;
            order[0].Add(Random.Range(4, 6));
            DebugMsg(ingredients[0][order[0][0]].Replace('\n', ' ') + ".");
        }
        else
        {
            if (Random.Range(0, 20) == 0)
                asMuch = true;
            /*else if (Random.Range(0, 25) == 0 && tipThreshold != 0)
            {
                vegetlblbgbelabe = true;
                int[] shuffledArray = { 0, 1, 2, 3, 4, 5 };
                int veggieCount = Random.Range(5, 7);
                shuffledArray.Shuffle(); 
                for (int j = 0; j < veggieCount; j++)
                    order[3].Add(shuffledArray[j]);
                requiredChangeValues += 3 * (veggieCount - 4);
            }*/

            // add bread (if there's not enough tip threshold, don't add whole wheat bread)
            if (requiredChangeValues + 3 <= tipThreshold)
            {
                order[0].Add(Random.Range(0, 3)); requiredChangeValues += 3;
            }
            else
            {
                order[0].Add(Random.Range(0, 4));
            }

            amounts = new int[] { 1, Random.Range(1, 4), Random.Range(1, 3), Random.Range(1, 6), Random.Range(1, 5) };

            int[] shuffledMeaties = { 0, 1, 2, 3, 4, 5 };
            int[] shuffledCheeses = { 0, 1, 2, 3, 4, 0 };
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

            /*ingredientCount = Random.Range(6, 10);
            if (vegetlblbgbelabe)
                ingredientCount = Random.Range(2, 5);

            for (int i = 0; i < ingredientCount; i++)
            {
                int placeholder = Random.Range(1, 5);
                if (order[1].Count == 0)
                    placeholder = 1;
                else if (order[2].Count == 0)
                    placeholder = 2;
                else if (order[3].Count == 0)
                    placeholder = 3;
                else if (order[4].Count == 0)
                    placeholder = 4;
                while (order[placeholder].Count >= 4 || (order[2].Count == 1 && placeholder == 2))
                    placeholder = Random.Range(1, 5);

                int placeholderIngredient = Random.Range(0, 6);

                switch (placeholder)
                {
                    case 1: // meats
                        while (order[placeholder].Contains(placeholderIngredient) || (order[4].Contains(1) && placeholderIngredient == 0))
                            placeholderIngredient = Random.Range(0, 6);
                            break;
                    case 2: // cheese
                        while (order[placeholder].Contains(placeholderIngredient))
                            placeholderIngredient = Random.Range(0, 5);
                        break;
                    case 3: // veggies
                        while (order[placeholder].Contains(placeholderIngredient))
                            placeholderIngredient = Random.Range(0, 6);
                        break;
                    case 4: // condiments
                        while (order[placeholder].Contains(placeholderIngredient) || (requiredChangeValues + 1 <= tipThreshold && placeholderIngredient == 0) || (order[1].Contains(0) && placeholderIngredient == 1))
                            placeholderIngredient = Random.Range(0, 6);
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
        
        if (Random.Range(0, 10) == 0) { order[2].Add(5); melt = true; DebugMsg("Also, the customer asked for a melt! You should TOAST THE BREAD. Or not."); }
        else if (Random.Range(0, 10) == 0) { order[2].Add(5); DebugMsg("Also, the customer asked you to TOAST THE BREAD."); }
        if (asMuch) {
            asMuchType = Random.Range(1, 5);
            asMuchPos = Random.Range(0, amounts[asMuchType]);
            while ((asMuchType == 3) && (order[asMuchType][asMuchPos] == 5))
            {
                asMuchType = Random.Range(1, 5);
                asMuchPos = Random.Range(0, amounts[asMuchType]);
            }
            /* while (order[asMuchType][asMuchPos] == 0 && (asMuchType == 1 || asMuchType == 4))
            {
                asMuchType = Random.Range(1, 5);
                asMuchPos = Random.Range(0, order[asMuchType].Count);
            } */
            // DebugMsg(asMuchType + "type and " + asMuchPos + "pos");
            DebugMsg("Also, the customer asked for as much " + ingredients[asMuchType][order[asMuchType][asMuchPos]] + " that will get you fired! Spam that button when you get to that category.");
        }
        if (pizzaTime) { DebugMsg("Also, the customer asked for a pizza! Get that customer a piece-a' pizza."); };
        if (order[0].Contains(6)) { DebugMsg("Also, the customer asked for whole wheat bread! Change it."); }
        if (order[4].Contains(0)) { DebugMsg("Also, the customer asked for ketchup! Either remove it or change it."); }
        if (order[1].Contains(0)) { replaceTuna = true; DebugMsg("Also, the customer asked for tuna! Make it mayonnaise."); }
        // if (order[3].Count > 6) { DebugMsg("Also, the customer asked for more than 6 veggies! Get rid of them until there's 6."); }
        if (requiredChangeValues <= tipThreshold) { changeCheese = true; DebugMsg("Also, there's still tip threshold remaining after all required changes! Change the cheese."); requiredChangeValues += 1; }

        // order[1].Remove(0);
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
                    if(!(sandwichMade[0].Contains(4) || sandwichMade[0].Contains(5)) || ((sandwichMade[0].Contains(0) || sandwichMade[0].Contains(1) || sandwichMade[0].Contains(2) || sandwichMade[0].Contains(3))))
                    {
                        sandwichCorrect = false;
                    }
                }
                else
                {
                    if (sandwichMade[0].Count == 0) { sandwichCorrect = false; yield return ATONEFORYOURSINS(); }
                    if (sandwichMade[3].Count == 0) { sandwichCorrect = false; reasonsWhy.Add("... you got rid of the last vegetable on the sandwich!"); }
                    if (asMuch) { sandwichCorrect = false; reasonsWhy.Add("... you didn't spam the " + ingredients[asMuchType][order[asMuchType][asMuchPos]] + " button!"); }
                    if (sandwichMade[0].Contains(6)) { sandwichCorrect = false; reasonsWhy.Add("... you gave the customer whole wheat bread!"); }
                    if (sandwichMade[4].Contains(0) && order[4].Contains(0)) { sandwichCorrect = false; reasonsWhy.Add("... you gave the customer ketchup when they asked for it!"); }
                    if (order[1].Contains(0) && (sandwichMade[1].Contains(0) || !sandwichMade[4].Contains(1))) { sandwichCorrect = false; reasonsWhy.Add("... you didn't replace the tuna with mayonnaise!"); } // this might be weird. you can't change/remove the substituted mayo
                    // if (sandwichMade[3].Count > 6 && order[3].Count > 6) { sandwichCorrect = false; reasonsWhy.Add("... you didn't get rid of veggies until there were less than 6 on the sandwich!"); }
                    // if (changeCheese && order[2].Where(x => x != 5) == sandwichMade[2].Where(x => x != 5)) { sandwichCorrect = false; reasonsWhy.Add("... you didn't change the cheese when you had the chance!"); }
                    if (order[0].Count < sandwichMade[0].Count || order[1].Count < sandwichMade[1].Count || order[2].Count < sandwichMade[2].Count || order[3].Count < sandwichMade[3].Count || (replaceTuna && order[4].Count < sandwichMade[4].Count - 1) || (!replaceTuna && order[4].Count < sandwichMade[4].Count)) { sandwichCorrect = false; reasonsWhy.Add("... you added an extra ingredient without removing an equivalent one!"); }

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
                                Debug.LogFormat("{0} {1}", ingredient, sandwichMade[i].Contains(ingredient));
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

                    for (int i = 0; i < 5; i++)
                    {
                        Debug.LogFormat("{0} Additions / {1}", i, additions[i]);
                        Debug.LogFormat("{0} Changes / {1}", i, changes[i]);
                        Debug.LogFormat("{0} Deletions / {1}", i, deletions[i]);
                    }

                    if (changeCheese && changes[2] == 0)
                    {
                        sandwichCorrect = false;
                        reasonsWhy.Add("... you didn't change the cheese when you had the chance!");
                    }

                    DebugMsg("The total value of your changes is " + changeValues + ".");

                    if ((changeValues > tipThreshold + 9) ? replaceTuna : (changeValues > tipThreshold))
                    {
                        sandwichCorrect = false;
                        reasonsWhy.Add("... you went over the tip threshold!");
                    }
                    else if (changeValues < tipThreshold)
                    {
                        int remainingThreshold = tipThreshold - requiredChangeValues;
                        int[] tempModifications = { 0, 0, 0, 0, 0 };
                        int[] tempChanges = { 0, 0, 0, 0, 0 };
                        int[] tempRemovals = { 0, 0, 0, 0, 0 };

                        tempModifications[3]++; // cannot remove the last vegetable
                        if (order[0].Contains(6)) { tempModifications[0]++; } // always change whole wheat
                        if (order[4].Contains(0)) { tempModifications[4]++; } // always remove ketchup
                        // if (replaceTuna) { tempModifications[4]++; } // replace tuna with mayo, can't re-change the mayo
                        // if (order[3].Count > 6) { tempModifications[3] += order[3].Count - 6; } // remove veggies until there are 6
                        if (changeCheese) { tempModifications[2]++; }

                        while (remainingThreshold >= 9 && order[1].Count() - tempModifications[1] > 0) { tempModifications[1]++; tempRemovals[1]++; remainingThreshold -= 9; } // remove meat
                        while (remainingThreshold >= 4 && order[2].Count() - tempModifications[2] > 0) { tempModifications[2]++; tempRemovals[2]++; remainingThreshold -= 4; } // remove cheese
                        while (remainingThreshold >= 4 && order[1].Count() - tempModifications[1] > 0) { tempModifications[1]++; tempChanges[1]++; remainingThreshold -= 4; } // change meat
                        while (remainingThreshold >= 3 && order[0].Count() - tempModifications[0] > 0) { tempModifications[0]++; tempChanges[0]++; remainingThreshold -= 3; } // change bread
                        while (remainingThreshold >= 3 && order[3].Count() - tempModifications[3] > 0) { tempModifications[3]++; tempRemovals[3]++; remainingThreshold -= 3; } // remove veggie
                        while (remainingThreshold >= 3 && order[4].Count() - tempModifications[4] > 0) { tempModifications[4]++; tempChanges[4]++; remainingThreshold -= 3; } // change condiment
                        while (remainingThreshold >= 2 && order[3].Count() - tempModifications[3] > 0) { tempModifications[3]++; tempChanges[3]++; remainingThreshold -= 2; } // change veggie
                        while (remainingThreshold >= 1 && order[2].Count() - tempModifications[2] > 0) { tempModifications[2]++; tempChanges[2]++; remainingThreshold -= 1; } // change cheese
                        while (remainingThreshold >= 1 && order[4].Count() - tempModifications[4] > 0) { tempModifications[4]++; tempRemovals[4]++; remainingThreshold -= 1; } // remove condiment

                        if ((remainingThreshold == tipThreshold - changeValues + 9) ? replaceTuna : (remainingThreshold == tipThreshold - changeValues))
                        {
                            DebugMsg("Your sandwich is valid and is close enough to the tip threshold!");
                            sandwichCorrect = true;
                        }   
                        else
                        {
                            reasonsWhy.Add("... your isn't close enough to the tip threshold!");
                            DebugMsg("Calculated solution:");
                            for (int i = 0; i < 5; i++)
                            {
                                if (tempChanges[i] != 0)
                                    DebugMsg("Change " + tempChanges[i] + " " + stageNames[i] + "...");
                                if (tempRemovals[i] != 0)
                                    DebugMsg("Remove " + tempRemovals[i] + " " + stageNames[i] + "...");
                            }

                            if (replaceTuna) // put the tuna back
                            {
                                order[4].Remove(1);
                                order[1].Add(0);
                            }
                        }
                    }
                    else
                    {
                        DebugMsg("Your sandwich is valid and is close enough to the tip threshold!");
                        sandwichCorrect = true;
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
                    DebugMsg("Your sandwhich was not valid because:");
                    foreach (var reason in reasonsWhy)
                    {
                        DebugMsg(reason);
                    }
                    Audio.PlaySoundAtTransform("failure" + speaker, Module.transform);
                    yield return new WaitForSeconds(voiceLines[speaker * 39 + 5].length);
                    Module.HandleStrike();
                }

                currentStage = 0;
                for (int i = 0; i < 5; i++) { sandwichMade[i].Clear(); }
                orderActivated = false;
                buttonText.text = "ORDER";
            }
        }

        ingredientPosition = Random.Range(0, 6);
        MoveIngredient(0);
    }
    
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
            if (sandwichMade[2].Contains(9))
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
}
