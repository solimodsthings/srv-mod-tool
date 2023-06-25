using System;

namespace SRVModTool
{
    /// <summary>
    /// A return value for methods that need to return a bool to 
    /// indicate an operation's success or failure, but also want 
    /// to say why an operation failed if it did so.
    /// </summary>
    public class Result
    {
        /// <summary>
        /// True if the operation was a success, otherwise false. If false,
        /// <see cref="ErrorMessage">ErrorMessage</see> will be populated with the reason.
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// The reason why the operation failed. This will only be populated
        /// if <see cref="IsSuccessful">IsSuccessful</see> evaluates to false.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
