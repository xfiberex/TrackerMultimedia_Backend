using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Contracts.Auth;

public record RefreshRequest([Required] string RefreshToken);
