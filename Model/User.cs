// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
namespace playground_check_service.Model
{
    public class User
    {
        public int fid { get; set; } = -1;
        public string mailAddress { get; set; } = "";
        public string passPhrase { get; set;} = "";
        public string lastName { get; set;} = "";
        public string firstName { get; set;} = "";
        public bool active { get; set; } = false;
        public DateTime? lastLoginAttempt { get; set; }
        public DateTime? databaseTime { get; set; }
        public string role { get; set; } = "";
        public string errorMessage { get; set; } = "";
        public bool isNew { get; set; } = false;
    }
}
