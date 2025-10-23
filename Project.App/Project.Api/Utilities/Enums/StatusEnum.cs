namespace Project.Api.Utilities.Enums;

public enum Status
{
    Active, // currently playing the game
    Inactive, // did not move last turn, will be kicked next turn
    Away, // in the room, but not playing
    Left, // left the room
}
