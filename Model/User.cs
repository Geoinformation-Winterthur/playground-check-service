// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
namespace playground_check_service.Model
{
    public class User
    {
        public int fid { get; set; }
        public string mailAddress { get; set; }
        public string passPhrase { get; set;}
        public string lastName { get; set;}
        public string firstName { get; set;}
        public DateTime? lastLoginAttempt { get; set; }
        public DateTime? databaseTime { get; set; }
    }
}
