using Spectre.Console;
using OCore.Copilot.Core;
using OpenAI_API.Chat;
using System.ComponentModel.DataAnnotations;

var configFile = File.ReadAllLines("openapi.txtconfig");

var apiKey = configFile[0].Split(' ')[1].Trim();
var organization = configFile[1].Split(' ')[1].Trim();

if (apiKey == null)
{
    AnsiConsole.MarkupLine("[bold red]The configuration is missing an API key. Check the example.openapi.txtconfig-file[/]");
    return;
}

AnsiConsole.Write(
    new FigletText("OCore Copilot")
        .Centered()
        .Color(Color.Fuchsia));
AnsiConsole.WriteLine();

// Some long-lived values
string? title = null;
string? description = null;

Service.SetupApi(apiKey);

var stakeholder = new Persona(Service.CreateConversation(), "Stakeholder", Color.Yellow);
var teamLead = new Persona(Service.CreateConversation(), "Team Lead", Color.Gold1);
var developer = new Persona(Service.CreateConversation(), "Developer", Color.Aquamarine3);

// Workflow
var workflow = new List<string>
{
    "Business Case",
    "Domain Actors",
    "Domain Concepts",
    "System Description",
    "UseCases",
    "TeamLead Reaction",
    "Task List",
    "Events",
    "Services",
    "DataEntities",
    "Developer Reactions",
    "Service Implementation",
};

// Store the output from an iteration over a concept so it can be propagated
var conceptResponses = new Dictionary<string, string>();

// Given a concept, define the values and relevant actors
var conceptRequests = new Dictionary<string, ConceptRequest>
{
    {
        "Business Case",
        new ConceptRequest(
            "Business Case",
            "[green]Let's talk to our business person![/]",
            stakeholder,
            new List<string> {"BusinessCase" },
            null,
            "I want you to help elaborate on the business case and give me an elevator pitch.") },
    {
        "Domain Actors",
        new ConceptRequest("Domain Actors",
            "Great! Let's indentify some [green]domain actors[/]",
            stakeholder,
            new List<string> { "DomainActors" }          
        )
    },
    {
        "Domain Concepts",
        new ConceptRequest("Domain Concepts",
            "Great! Let's indentify some [green]domain concepts[/]",
            stakeholder,
            new List<string> { "DomainConcepts" }            
        )
    },
    {
        "System Description",
        new ConceptRequest("System Description",
            "Great! Let's try to describe the system [green]in a developer friendly way[/]",
            stakeholder,
            new List<string> { "SystemDescription" }            
        )
    },        
    {
        "UseCases",
        new ConceptRequest("UseCases",
            "Great! Let's try to [green]describe some use-cases[/] so we can get started on development",
            stakeholder,
            new List<string> { "UseCases" }      
        )
    }, 
    {
        "TeamLead Reaction",
        new ConceptRequest("TeamLead Reaction",
            "[green]Let's get the team lead involved in the process![/]",
            teamLead,
            new List<string> { "Developer", "TeamLead" },
            new List<string> { "Business Case", "Domain Actors", "Domain Concepts", "System Description", "UseCases" }
        )
    },
    {
        "Task List",
        new ConceptRequest("Task List",
            "[green]Create some concrete tasks that a developer can get started on.[/]",
            teamLead,
            new List<string> { "TaskCreation" }            
        )
    }, 
    {
        "Events",
        new ConceptRequest("Events",
            "[green]Can you identify events in the system?[/]",
            teamLead,
            new List<string> { "Events" },
            Prompt: "Identify the Events in the system"
        )
    },
    {
        "Services",
        new ConceptRequest("Services",
            "[green]Can you identify services in the system?[/]",
            teamLead,
            new List<string> { "Services" },
            Prompt: "Identify the Services in the system"
        )
    },
    {
        "DataEntities",
        new ConceptRequest("DataEntities",
            "[green]Can you identify data entities in the system?[/]",
            teamLead,
            new List<string> { "DataEntities" },
            Prompt: "Identify the DataEntities in the system"
        )
    },
    {
        "Developer Reactions",
        new ConceptRequest("Developer Reactions",
            "[green]Let's talk to some developers![/]",
            developer,
            new List<string> { 
                "Developer", 
                "OCore.Communication", 
                "OCore.Service.Code", 
                "OCore.Event.Code",
                "OCore.DataEntity.Code"
            },
            new List<string>
            {
                "Task List",
                "Domain Actors",
                "Domain Concepts",
                "UseCases",
                "Services",
                "DataEntities",
                "Events"
            },
            "Give me your initial reaction to these tasks, how they are defined and how comprehensible they are."
        )
    },
        {
        "Service Implementation",
        new ConceptRequest("Service Implementation",
            "[green]Let's try to implement some of the services![/]",
            developer,
            Prompt: "Implement the relevant services"
        )
    },
};

// Given a concept, if appropriate, make a call to a func that indicates keys to be
// interpolated
var interpolationKeys = new Dictionary<string, Func<List<Tuple<string, string>>>>
{
    { "Business Case", () =>
        {
            return new List<Tuple<string, string>>{
                Tuple.Create("Title", title!),
                Tuple.Create("Description", description!)
            };
        }
    }
};

// Let's just support new business cases for now
await NewBusinessCase();

// Execute necessary steps for a concept
async Task RunConcept(string conceptName)
{
    if (conceptRequests!.TryGetValue(conceptName, out var conceptRequest))
    {
        AnsiConsole.MarkupLine(conceptRequest.Introduction);

        // Prep the conversation, first the previous concepts
        if (conceptRequest.InputConcepts != null)
        {
            foreach (var concept in conceptRequest.InputConcepts)
            {
                conceptRequest.Persona.Conversation.AppendSystemMessage($"{conceptName}: {conceptResponses![concept]}");
            }
        }

        // Prep the conversation, then the current instructions
        if (conceptRequest.Instructions != null)
        {
            foreach (var instructionsName in conceptRequest.Instructions)
            {
                var instructions = GetInstructions(instructionsName);

                // Interpolate the instructions if necessary
                if (interpolationKeys!.TryGetValue(conceptName, out var interpolationKeyFunc))
                {
                    var interpolationKeys = interpolationKeyFunc();
                    instructions = Interpolate(instructions, interpolationKeys);
                }

                conceptRequest.Persona.Conversation.AppendSystemMessage(instructions);
            }
        }


        // The conversation should be properly prepped, let's just run the iteration
        var conceptResponse = await Iteration(conceptRequest.Persona.Conversation,
            conceptName,
            conceptRequest.Prompt,
            conceptRequest.Persona.Name,
            conceptRequest.Persona.Color);

        conceptResponses!.Add(conceptName, conceptResponse);
    }
    else
    {
        throw new Exception("Undefined concept, there seems to be something wrong");
    }
}

async Task NewBusinessCase()
{
    var correctTandB = false;

    while (correctTandB == false)
    {
        // This got clunky, the API for Spectre seems a little odd
        if (title == null)
        {
            title = AnsiConsole.Ask<string>("What is the [purple]title[/] of your project?");
        }
        else
        {
            title = AnsiConsole.Ask<string>("What is the [purple]title[/] of your project?", title);
        }

        if (description == null)
        {
            description = AnsiConsole.Ask<string>("Can you give an overall description of your [green]business case[/]?");
        }
        else
        {
            description = AnsiConsole.Ask<string>("Can you give an overall description of your [green]business case[/]?", description);
        }

        var topTable = new Table();

        topTable.AddColumn(title);
        topTable.AddRow(description);

        AnsiConsole.Write(topTable);

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description))
        {
            AnsiConsole.MarkupLine("[red]Values cannot be empty[/]");
            continue;
        }

        correctTandB = AnsiConsole.Confirm("Does this look correct?");
        if (correctTandB == false)
        {
            AnsiConsole.MarkupLine("Let's give it a little more love!");
        }
    }

    if (Directory.Exists(Path.Combine("artifacts", title!)) == false)
    {
        Directory.CreateDirectory(Path.Combine("artifacts", title!));
    }

    foreach (var conceptName in workflow)
    {
        await RunConcept(conceptName);
    }

    // This negotiation-part between stakeholder and team lead is currently commented out
    // as it quickly pushes against the token limit for gpt-3-turbo

    //var uncertaintyInstructions = GetInstructions("StakeholderUncertainties");
    //var interpolatedUncertainties = Interpolate(uncertaintyInstructions, ("TaskList", taskList));
    //var resolvedUncertainties = await Iteration(businessPerson, interpolatedUncertainties, "BusinessPerson", businessPersonColor);

    //var resolvedTaskListInstructions = GetInstructions("ResolvedTasklist");
    //var interpolatedResolvedTasklist = Interpolate(resolvedTaskListInstructions, ("ResolvedUncertainties", resolvedUncertainties));

    //var addedAnswers = await Iteration(teamLead, interpolatedResolvedTasklist, "Team Lead", teamLeadColor);
    

    

}

string Interpolate(string businessCaseInstructions, List<Tuple<string, string>> substitutes)
{
    if (substitutes == null) return businessCaseInstructions;
    foreach (var substitute in substitutes)
    {
        businessCaseInstructions = businessCaseInstructions.Replace($"{{{substitute.Item1}}}", substitute.Item2);
    }
    return businessCaseInstructions;
}

string GetInstructions(string instructionsName)
{
    var instructions = File.ReadAllText(Path.Combine("Instructions", $"{instructionsName}.txt"));
    return instructions;
}


async Task<string> Iteration(Conversation conversation,
    string conceptName,
    string? prompt,
    string actorName,
    Color color,
    string? happyQuestion = null,
    string? reminder = null)
{
    var path = Path.Combine("artifacts", title!, $"{conceptName}.txt");
    string returnString = string.Empty;
    if (File.Exists(path))
    {
        returnString = File.ReadAllText(path);
    }

    var happy = false;
    if (returnString == string.Empty)
    {
        if (string.IsNullOrEmpty(prompt) == false)
        {
            Service.AddInput(conversation, prompt);
        }
    }

    do
    {
        AnsiConsole.MarkupInterpolated($"[{color}]<{actorName}>[/]");
        AnsiConsole.WriteLine();

        if (returnString != string.Empty)
        {
            AnsiConsole.MarkupLineInterpolated($"[{color}]{returnString}[/]");
        }
        else
        {

            try
            {
                await foreach (var segment in Service.GetStream(conversation))
                {
                    if (segment != null)
                    {
                        AnsiConsole.MarkupInterpolated($"[{color}]{segment}[/]");
                        returnString += segment;
                    }
                }
            }
            catch (Exception ex)
            {
                // Just retry
                AnsiConsole.MarkupLine($"[red]Trouble reading from conversation stream, retrying...{ex.Message}[/]");
                returnString = string.Empty;
                continue;
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        happy = AnsiConsole.Confirm(happyQuestion ?? "Are you happy?");
        if (happy == false)
        {
            var feedback = AnsiConsole.Ask<string>("Please elaborate: ");
            if (reminder != null)
            {
                Service.AddInput(conversation, reminder);
            }
            Service.AddInput(conversation, feedback);
            returnString = string.Empty;
        }
    } while (happy == false);

    File.WriteAllText(path, returnString);

    return returnString;
}

record Persona(
    Conversation Conversation,
    string Name,
    Color Color);

record ConceptRequest(
    string ConceptName,
    string Introduction,
    Persona Persona,
    List<string>? Instructions = null,
    List<string>? InputConcepts = null,
    string? Prompt = null);