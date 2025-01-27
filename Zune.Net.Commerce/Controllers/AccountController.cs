﻿using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Cryptography;
using System.Text;
using Zune.DB;
using Zune.Xml.Commerce;

namespace CommerceZuneNet.Controllers
{
    [Route("/{version}/{language}/account/{action=SignIn}")]
    [Route("/{language}/account/{action=SignIn}")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly ZuneNetContext _database;
        public AccountController(ZuneNetContext database)
        {
            _database = database;
        }

        [HttpPost]
        public ActionResult<SignInResponse> SignIn(SignInRequest request)
        {
            // TODO: Authentication is a mess. SignInRequest only provides a (device?)
            // ID that is only seen in this endpoint. After that, all subsequent
            // requests use a Zune tag or GUID to specify a member, and a WLID
            // token for authorization. Most Commerce endpoints just send the WLID,
            // which doesn't directly identify which user's data is being requested.
            var zuneId = Zune.DB.Models.Member.GetGuidFromZuneTag("YoshiAsk").ToString();
            var member = _database.Members.Find(zuneId);

            SignInResponse response;
            if (member != null)
            {
                response = member.GetSignInResponse();
            }
            else
            {
                // TODO: Work out the error response format
                response = new SignInResponse
                {
                    AccountState = new AccountState
                    {
                        SignInErrorCode = 0x80070057,
                    }
                };
            }

            return response;
        }

        [HttpPost]
        public ActionResult<GetUserResponse> User(GetUserRequest request)
        {
            return new(new GetUserResponse());
        }

        [HttpPost]
        public ActionResult<BalancesResponse> Balances()
        {
            return new BalancesResponse
            {
                Balances = new()
                {
                    PointsBalance = 8000,
                    SongCreditBalance = 0,
                    SongCreditRenewalDate = null
                }
            };
        }
    }
}
