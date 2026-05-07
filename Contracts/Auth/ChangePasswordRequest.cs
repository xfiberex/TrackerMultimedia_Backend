namespace TrackerMultimedia.Contracts.Auth;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
