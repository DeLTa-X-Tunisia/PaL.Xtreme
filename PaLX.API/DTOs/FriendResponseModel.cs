namespace PaLX.API.DTOs
{
    public class FriendResponseModel
    {
        public string Requester { get; set; }
        public int Response { get; set; } // 1: Accept, 0: Decline
    }
}
