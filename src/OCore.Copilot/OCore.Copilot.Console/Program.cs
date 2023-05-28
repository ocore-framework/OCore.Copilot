using Spectre.Console;
using OCore.Copilot.Core;
using System.Diagnostics;
using OpenAI_API.Chat;
using System.Drawing;

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
        .Color(Spectre.Console.Color.Fuchsia));
AnsiConsole.WriteLine();

const string newBusinessCase = "New business case";
const string iterateOnRepo = "Iterate on existing repo";

var operationSelected = false;
var programmingLanguage = "CSharp";

Conversation businessPerson = null;
string businessPersonColor = "yellow";
Conversation coder = null;
string coderColor = "gold1";

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
    var interpolatedDomainConceptInstructions = Interpolate(domainConceptInstructions, null);
    await IdentifyConcepts(businessPerson, interpolatedDomainConceptInstructions);

    var systemDescriptionInstructions = GetInstructions("SystemDescription");
    AnsiConsole.MarkupLine("Great! Let's try to [green]describe the system[/] in a developer friendly way");
    var interpolatedSystemDescriptionInstructions = Interpolate(systemDescriptionInstructions, null);
    await IdentifySystemDescription(businessPerson, interpolatedSystemDescriptionInstructions);

    var useCasesInstructions = GetInstructions("UseCases");
    AnsiConsole.MarkupLine("Great! Let's try to [green]describe some use-cases[/] so we can get started on development");
    var interpolatedUseCasesInstructions = Interpolate(useCasesInstructions, null);
    await IdentifyUseCases(businessPerson, interpolatedUseCasesInstructions);

    AnsiConsole.MarkupLine("[green]Let's get some developers into the process![/]");
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
    var instructionLines = File.ReadAllLines(Path.Combine("Instructions", $"{instructionName}.txt"));
    var instructions = string.Join(' ', instructionLines);
    return instructions;
}

async Task CreateBusinessCase(Conversation businessPerson, string interpolatedBusinessCaseInstructions)
{
    var happyWithBusinessCase = false;
    Service.AddSystemMessage(businessPerson, interpolatedBusinessCaseInstructions);
    Service.AddInput(businessPerson, "I want you to help elaborate on the business case and give me an elevator pitch.");

    AnsiConsole.Markup($"[{businessPersonColor}]<BusinessPerson>[/]: ");

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
    string actorName, 
    string color,
    string? happyQuestion = null,
    string? reminder = null)
{
    var happy = false;
    Service.AddInput(conversation, initialPrompt);

    do
    {
        AnsiConsole.Markup($"[{color}]<{actorName}>[/]: ");
        await foreach (var segment in Service.GetStream(conversation))
        {
            if (segment != null)
            {
                AnsiConsole.Markup($"[{color}]{segment}[/]");
            }
        }
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        happy = AnsiConsole.Confirm(happyQuestion ?? "Are you happy?");
        if (happy == false)
        {
            var prompt = AnsiConsole.Ask<string>("Please elaborate: ");
            if (reminder != null)
            {
                Service.AddInput(conversation, reminder);
            }
            Service.AddInput(conversation, prompt);
        }
    } while (happy == false);
}

async Task IdentifyActors(Conversation businessPerson, string interpolatedDomainActors)
{
    await Iteration(businessPerson, interpolatedDomainActors, "BusinessPerson", businessPersonColor, "Are you happy with the identified actors?", "Remember to only output these in the previously described format, just the list, no chatter outside the list.");
}

async Task IdentifyConcepts(Conversation businessPerson, string interpolatedDomainConcepts)
{
    await Iteration(businessPerson, interpolatedDomainConcepts, "BusinessPerson", businessPersonColor, "Are you happy with the identified concepts?", "Remember to only output these in the previously described format, just the list, no chatter outside the list.");
}

async Task IdentifySystemDescription(Conversation businessPerson, string interpolatedSystemDescription)
{
    await Iteration(businessPerson, interpolatedSystemDescription, "BusinessPerson", businessPersonColor, "Are you happy with the proposed system description?");
}

async Task IdentifyUseCases(Conversation businessPerson, string interpolatedUseCases)
{
    await Iteration(businessPerson, interpolatedUseCases, "BusinessPerson", businessPersonColor, "Are you happy with these usecases?");
}