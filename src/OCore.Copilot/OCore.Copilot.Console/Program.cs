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

AnsiConsole.Write(
    new FigletText("OCore Copilot")
        .Centered()
        .Color(Spectre.Console.Color.Fuchsia));
AnsiConsole.WriteLine();

const string newBusinessCase = "New business case";
const string iterateOnRepo = "Iterate on existing repo";

var operationSelected = false;
var programmingLanguage = "CSharp";

Conversation? businessPerson = null;
string businessPersonColor = "yellow";
Conversation? teamLead = null;
string teamLeadColor = "gold1";
Conversation? seniorDeveloper = null;
string seniorDeveloperColor = "aquamarine3";

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

    Service.AddSystemMessage(businessPerson, interpolatedBusinessCaseInstructions);
    
    var businessCase = await CreateBusinessCase(businessPerson, "I want you to help elaborate on the business case and give me an elevator pitch.");

    var domainActorInstructions = GetInstructions("DomainActors");

    AnsiConsole.MarkupLine("Great! Let's indentify some [green]domain actors[/]");
    var interpolatedDomainActorInstructions = Interpolate(domainActorInstructions, null);
    var actors = await IdentifyActors(businessPerson, interpolatedDomainActorInstructions);

    var domainConceptInstructions = GetInstructions("DomainConcepts");
    AnsiConsole.MarkupLine("Great! Let's indentify some [green]domain concepts[/]");
    var interpolatedDomainConceptInstructions = Interpolate(domainConceptInstructions, null);
    var concepts = await IdentifyConcepts(businessPerson, interpolatedDomainConceptInstructions);

    var systemDescriptionInstructions = GetInstructions("SystemDescription");
    AnsiConsole.MarkupLine("Great! Let's try to [green]describe the system[/] in a developer friendly way");
    var interpolatedSystemDescriptionInstructions = Interpolate(systemDescriptionInstructions, null);
    var systemDescription = await IdentifySystemDescription(businessPerson, interpolatedSystemDescriptionInstructions);

    var useCasesInstructions = GetInstructions("UseCases");
    AnsiConsole.MarkupLine("Great! Let's try to [green]describe some use-cases[/] so we can get started on development");
    var interpolatedUseCasesInstructions = Interpolate(useCasesInstructions, null);
    var useCases = await IdentifyUseCases(businessPerson, interpolatedUseCasesInstructions);

    AnsiConsole.MarkupLine("[green]Let's get some developers into the process![/]");

    teamLead = Service.CreateConversation();
    var teamLeadInstructions = GetInstructions("TeamLead");
    var interpolatedTeamLeadInstructions = Interpolate(teamLeadInstructions, null);
    
    Service.AddSystemMessage(teamLead, interpolatedTeamLeadInstructions);
    Service.AddSystemMessage(teamLead, $"The business case is: {businessCase}");
    Service.AddSystemMessage(teamLead, $"The domain actors are: {actors}");
    Service.AddSystemMessage(teamLead, $"The domain concepts are: {concepts}");
    Service.AddSystemMessage(teamLead, $"The system description is: {systemDescription}");
    Service.AddSystemMessage(teamLead, $"The use cases are: {useCases}");
    
    await GetInitialTeamLeadReaction(teamLead, "Can you give me your initial reactions on this?");

    
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
#if false
    var instructionLines = File.ReadAllLines(Path.Combine("Instructions", $"{instructionName}.txt"));
    var instructions = string.Join(' ', instructionLines);
#endif
    var instructions = File.ReadAllText(Path.Combine("Instructions", $"{instructionName}.txt"));
    return instructions;
}


static async Task<string> Iteration(Conversation conversation,
    string initialPrompt,
    string actorName, 
    string color,
    string? happyQuestion = null,
    string? reminder = null)
{
    var happy = false;
    Service.AddInput(conversation, initialPrompt);
    string returnString = string.Empty;
    
    do
    {
        AnsiConsole.Markup($"[{color}]<{actorName}>:[/] ");
        await foreach (var segment in Service.GetStream(conversation))
        {
            if (segment != null)
            {
                AnsiConsole.Markup($"[{color}]{segment}[/]");
                returnString += segment;
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

    return returnString;
}


async Task<string>  GetInitialTeamLeadReaction(Conversation conversation, string query)
{
    return await Iteration(conversation, query, "Team Lead", teamLeadColor);
}

async Task<string> CreateBusinessCase(Conversation businessPerson, string interpolatedBusinessCaseInstructions)
{
    return await Iteration(businessPerson, interpolatedBusinessCaseInstructions, "BusinessPerson", businessPersonColor, "Are you happy with the business case description?");
}

async Task<string> IdentifyActors(Conversation businessPerson, string interpolatedDomainActors)
{
    return await Iteration(businessPerson, interpolatedDomainActors, "BusinessPerson", businessPersonColor, "Are you happy with the identified actors?", "Remember to only output these in the previously described format, just the list, no chatter outside the list.");
}

async Task<string> IdentifyConcepts(Conversation businessPerson, string interpolatedDomainConcepts)
{
    return await Iteration(businessPerson, interpolatedDomainConcepts, "BusinessPerson", businessPersonColor, "Are you happy with the identified concepts?", "Remember to only output these in the previously described format, just the list, no chatter outside the list.");
}

async Task<string> IdentifySystemDescription(Conversation businessPerson, string interpolatedSystemDescription)
{
    return await Iteration(businessPerson, interpolatedSystemDescription, "BusinessPerson", businessPersonColor, "Are you happy with the proposed system description?");
}

async Task<string> IdentifyUseCases(Conversation businessPerson, string interpolatedUseCases)
{
    return await Iteration(businessPerson, interpolatedUseCases, "BusinessPerson", businessPersonColor, "Are you happy with these usecases?");
}