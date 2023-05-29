using Spectre.Console;
using Spectre.Console.Rendering;
using OCore.Copilot.Core;
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
string? title = null;

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

    if (Directory.Exists(title) == false)
    {
        Directory.CreateDirectory(title!);
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

    AnsiConsole.MarkupLine("[green]Let's get the team lead involved in the process![/]");

    teamLead = Service.CreateConversation();
    var teamLeadInstructions = GetInstructions("TeamLead");
    var developerInstructions = GetInstructions("Developer");
    var interpolatedTeamLeadInstructions = Interpolate(teamLeadInstructions, null);
    var interpolatedDeveloperIntructions = Interpolate(developerInstructions, null);

    Service.AddSystemMessage(teamLead, interpolatedTeamLeadInstructions);
    Service.AddSystemMessage(teamLead, interpolatedDeveloperIntructions);
    //Service.AddSystemMessage(teamLead, $"The business case is: {businessCase}");
    Service.AddSystemMessage(teamLead, $"The domain actors are: {actors}");
    Service.AddSystemMessage(teamLead, $"The domain concepts are: {concepts}");
    Service.AddSystemMessage(teamLead, $"The system description is: {systemDescription}");
    Service.AddSystemMessage(teamLead, $"The use cases are: {useCases}");


    var initialTeamLeadReaction = await GetInitialTeamLeadReaction(teamLead, "Can you give me your initial reactions on this?");

    AnsiConsole.MarkupLine("[green]Create some concrete tasks that a developer can get started on.[/]");

    var taskInstructions = GetInstructions("TaskCreation");
    var interpolatedTaskInstructions = Interpolate(taskInstructions, null);
    var taskList = await TaskCreation(teamLead, interpolatedTaskInstructions);

    //var uncertaintyInstructions = GetInstructions("StakeholderUncertainties");
    //var interpolatedUncertainties = Interpolate(uncertaintyInstructions, ("TaskList", taskList));
    //var resolvedUncertainties = await Iteration(businessPerson, interpolatedUncertainties, "BusinessPerson", businessPersonColor);

    //var resolvedTaskListInstructions = GetInstructions("ResolvedTasklist");
    //var interpolatedResolvedTasklist = Interpolate(resolvedTaskListInstructions, ("ResolvedUncertainties", resolvedUncertainties));

    //var addedAnswers = await Iteration(teamLead, interpolatedResolvedTasklist, "Team Lead", teamLeadColor);

    AnsiConsole.MarkupLine("[green]Can you identify events in the system?[/]");
    var eventInstructions = GetInstructions("Events");
    var interpolatedEventInstructions = Interpolate(eventInstructions, null);
    var eventList = await Iteration(teamLead, "events", interpolatedEventInstructions, "Team Lead", teamLeadColor);

    AnsiConsole.MarkupLine("[green]Can you identify services in the system?[/]");
    var serviceInstructions = GetInstructions("Services");
    var interpolatedServiceInstructions = Interpolate(serviceInstructions, null);
    var serviceList = await Iteration(teamLead, "services", interpolatedServiceInstructions, "Team Lead", teamLeadColor);

    AnsiConsole.MarkupLine("[green]Can you identify data entities in the system?[/]");
    var dataEntityInstructions = GetInstructions("DataEntities");
    var interpolatedDataEntityInstructions = Interpolate(dataEntityInstructions, null);
    var dataEntityList = await Iteration(teamLead, "dataentities", interpolatedDataEntityInstructions, "Team Lead", teamLeadColor);

    AnsiConsole.MarkupLine("[green]Let's talk to some developers![/]");

    seniorDeveloper = Service.CreateConversation();
    var ocoreCommunicationInstructions = GetInstructions("OCore.Communication");
    var interpolatedCommunicationInstructions = Interpolate(ocoreCommunicationInstructions, null);

    Service.AddSystemMessage(seniorDeveloper, interpolatedDeveloperIntructions);
    Service.AddSystemMessage(seniorDeveloper, interpolatedCommunicationInstructions);

    var serviceCodeInstructions = GetInstructions("OCore.Service.Code");
    var interpolatedServiceCodeInstructions = Interpolate(serviceCodeInstructions, ("Title", title!));
    Service.AddSystemMessage(seniorDeveloper, $"These are the Services: {serviceList}");
    Service.AddSystemMessage(seniorDeveloper, interpolatedServiceCodeInstructions);
    var code = await Iteration(seniorDeveloper, "code", "Write the code for the identified services in C#", "Senior Developer", seniorDeveloperColor);


}

async Task<string> TaskCreation(Conversation conversation, string query)
{
    return await Iteration(conversation, "tasks", query, "Team Lead", teamLeadColor);
}

string Interpolate(string businessCaseInstructions, params (string, string)[]? substitutes)
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


async Task<string> Iteration(Conversation conversation,
    string conceptName,
    string initialPrompt,
    string actorName,
    string color,
    string? happyQuestion = null,
    string? reminder = null)
{
    var path = Path.Combine(title!, $"{conceptName}.txt");
    string returnString = string.Empty;
    if (File.Exists(path))
    {
        returnString = File.ReadAllText(path);        
    }

    var happy = false;
    if (returnString == string.Empty)
    {
        Service.AddInput(conversation, initialPrompt);
    }

    do
    {
        AnsiConsole.Markup($"[{color}]<{actorName}>:[/] ");

        if (returnString != string.Empty)
        {
            Console.WriteLine(returnString);
        }
        else
        {

            try
            {
                await foreach (var segment in Service.GetStream(conversation))
                {
                    if (segment != null)
                    {
                        Console.Write(segment);
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
            var prompt = AnsiConsole.Ask<string>("Please elaborate: ");
            if (reminder != null)
            {
                Service.AddInput(conversation, reminder);
            }
            Service.AddInput(conversation, prompt);
            returnString = string.Empty;
        }
    } while (happy == false);

    File.WriteAllText(path, returnString);

    return returnString;
}


async Task<string> GetInitialTeamLeadReaction(Conversation conversation, string query)
{
    return await Iteration(conversation, "leadreaction", query, "Team Lead", teamLeadColor);
}

async Task<string> CreateBusinessCase(Conversation businessPerson, string interpolatedBusinessCaseInstructions)
{
    return await Iteration(businessPerson, "businesscase", interpolatedBusinessCaseInstructions, "BusinessPerson", businessPersonColor, "Are you happy with the business case description?");
}

async Task<string> IdentifyActors(Conversation businessPerson, string interpolatedDomainActors)
{
    return await Iteration(businessPerson, "actors", interpolatedDomainActors, "BusinessPerson", businessPersonColor, "Are you happy with the identified actors?", "Remember to only output these in the previously described format, just the list, no chatter outside the list.");
}

async Task<string> IdentifyConcepts(Conversation businessPerson, string interpolatedDomainConcepts)
{
    return await Iteration(businessPerson, "domainconcepts", interpolatedDomainConcepts, "BusinessPerson", businessPersonColor, "Are you happy with the identified concepts?", "Remember to only output these in the previously described format, just the list, no chatter outside the list.");
}

async Task<string> IdentifySystemDescription(Conversation businessPerson, string interpolatedSystemDescription)
{
    return await Iteration(businessPerson, "systemdescription", interpolatedSystemDescription, "BusinessPerson", businessPersonColor, "Are you happy with the proposed system description?");
}

async Task<string> IdentifyUseCases(Conversation businessPerson, string interpolatedUseCases)
{
    return await Iteration(businessPerson, "usecases", interpolatedUseCases, "BusinessPerson", businessPersonColor, "Are you happy with these usecases?");
}