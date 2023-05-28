using Spectre.Console;
using OCore.Copilot.Core;
using System.Diagnostics;
using OpenAI_API.Chat;

var configFile = File.ReadAllLines("openapi.txtconfig");

var apiKey = configFile[0].Split(' ')[1].Trim();
var organization = configFile[1].Split(' ')[1].Trim();

if (apiKey == null)
{
    AnsiConsole.MarkupLine("[bold red]The configuration is missing an API key. Check the example.openapi.txtconfig-file[/]");
    return;
}

AnsiConsole.MarkupLine("[bold fuchsia]-=*> OCore Copilot <*=-[/]");
AnsiConsole.WriteLine();

const string newBusinessCase = "New business case";
const string iterateOnRepo = "Iterate on existing repo";

var operationSelected = false;
var programmingLanguage = "CSharp";

Conversation businessPerson = null;

Service.SetupApi(apiKey);

while (operationSelected == false)
{
    var operation = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold green]What are we doing?[/]")
            .AddChoices(new[]
            {
                newBusinessCase,
                iterateOnRepo
            }
        ));

    switch (operation)
    {
        case newBusinessCase:
            operationSelected = true;
            await NewBusinessCase();
            break;
        case iterateOnRepo:
            operationSelected = true;
            break;
        default:
            AnsiConsole.MarkupLine("[bold red]Strange, my friend, but you seem to have picked an invalid option[/]");
            break;
    }
}

async Task NewBusinessCase()
{
    var correctTandB = false;
    
    string? title = null;
    string? description = null;

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

    var businessCaseInstructions = GetInstructions("BusinessCase");
    var interpolatedBusinessCaseInstructions = Interpolate(businessCaseInstructions,
        ("Title", title!),
        ("Description", description!));


    businessPerson = Service.CreateConversation();

    AnsiConsole.MarkupLine("[green]Let's talk to our business person![/]");

    await CreateBusinessCase(businessPerson, interpolatedBusinessCaseInstructions);

    var domainActorInstructions = GetInstructions("DomainActors");

    AnsiConsole.MarkupLine("Great! Let's indentify some [green]domain actors[/]");
    var interpolatedDomainActorInstructions = Interpolate(domainActorInstructions, null);
    await IdentifyActors(businessPerson, interpolatedDomainActorInstructions);

    var domainConceptInstructions = GetInstructions("DomainConcepts");
    AnsiConsole.MarkupLine("Great! Let's indentify some [green]domain concepts[/]");
    var interpolatedDomainConceptInstructions = Interpolate(domainActorInstructions, null);
    await IdentifyActors(businessPerson, interpolatedDomainConceptInstructions);
}

string Interpolate(string businessCaseInstructions, params (string,string)[]? substitutes)
{
    if (substitutes == null) return businessCaseInstructions;
    foreach (var substitute in substitutes)
    {
        businessCaseInstructions = businessCaseInstructions.Replace($"{{{substitute.Item1}}}", substitute.Item2);
    }
    return businessCaseInstructions;
}

string GetInstructions(string instructionName)
{
    var instructions = File.ReadAllText(Path.Combine("Instructions", $"{instructionName}.txt"));
    return instructions;
}

static async Task CreateBusinessCase(Conversation businessPerson, string interpolatedBusinessCaseInstructions)
{
    var happyWithBusinessCase = false;
    Service.AddSystemMessage(businessPerson, interpolatedBusinessCaseInstructions);
    Service.AddInput(businessPerson, "I want you to help elaborate on the business case and give me an elevator pitch.");

    await foreach (var segment in Service.GetStream(businessPerson))
    {
        if (segment != null)
        {
            AnsiConsole.Markup($"[yellow]{segment}[/]");
        }
    }
    AnsiConsole.WriteLine();

    happyWithBusinessCase = AnsiConsole.Confirm("Are you happy with this description of the business case?");

    while (happyWithBusinessCase == false)
    {
        var elaboration = AnsiConsole.Ask<string>("Please elaborate (end with empty line): ");
        if (elaboration != null)
        {
            Service.AddInput(businessPerson, elaboration);
            await foreach (var segment in Service.GetStream(businessPerson))
            {
                if (segment != null)
                {
                    AnsiConsole.Markup($"[yellow]{segment}[/]");
                }
            }
            AnsiConsole.WriteLine();
        }
        else break;
    }    
}

static async Task Iteration(Conversation conversation, 
    string initialPrompt, 
    string? happyQuestion = null,
    string? reminder = null)
{
    var happy = false;
    Service.AddInput(conversation, initialPrompt);

    do
    {
        await foreach (var segment in Service.GetStream(conversation))
        {
            if (segment != null)
            {
                AnsiConsole.Write(segment);
            }
        }
        AnsiConsole.WriteLine();

        happy = AnsiConsole.Confirm(happyQuestion ?? "Are you happy with this description?");
        if (happy == false)
        {
            var prompt = AnsiConsole.Ask<string>("Please elaborate: ");
            Service.AddInput(conversation, prompt);
            if (reminder != null)
            {
                Service.AddInput(conversation, reminder);
            }
        }
    } while (happy == false);
}

static async Task IdentifyActors(Conversation businessPerson, string interpolatedDomainActors)
{
    await Iteration(businessPerson, interpolatedDomainActors, "Are you happy with the identified actors?", "Remember to only output these in the previously described format, just the list, no chatter outside the list.");
}

static async Task IdentifyConcepts(Conversation businessPerson, string interpolatedDomainConcepts)
{
    await Iteration(businessPerson, interpolatedDomainActors, "Are you happy with the identified concepts?", "Remember to only output these in the previously described format, just the list, no chatter outside the list.");
}