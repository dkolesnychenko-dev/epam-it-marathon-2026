namespace APIHandler.Exceptions;

public class GroupReservationNotFoundException(string message = "Group reservation was not found") : ValidationException(message);
