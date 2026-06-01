namespace Interfaces;

public record ControlMessage(ControlMessageCommands CommandName, decimal? Temperature);

public enum ControlMessageCommands
{
    Colder,
    Warmer,
}
