﻿using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using UnofficalSteamAuthenticator.Tests.Mock;
using UnofficialSteamAuthenticator.SteamAuth;

namespace UnofficalSteamAuthenticator.Tests.SteamAuth
{
    [TestClass]
    public class FinalizeAddAuthenticatorTest
    {
        private readonly SessionData sessionData;
        private AuthenticatorLinker linker;
        private const string TestSmsCode = "##SMSCODE##";
        private const string TestSteamGuardCode = "#CODE#";

        private readonly Func<Dictionary<string, string>, bool> checkSmsCode;
        private readonly Func<Dictionary<string, string>, bool> checkFinalize;

        public FinalizeAddAuthenticatorTest()
        {
            this.sessionData = new SessionData()
            {
                Username = "test-username",
                SteamID = 7656119800000000L,
                OAuthToken = "test-oauth",
                SessionID = "test-sessionid",
                SteamLogin = "test-steamlogin",
                SteamLoginSecure = "test-steamsecure",
                WebCookie = "test-webcookie"
            };

            this.checkSmsCode = postData =>
                postData["op"].Equals("check_sms_code") &&
                postData["arg"].Equals(TestSmsCode) &&
                postData["sessionid"].Equals("test-sessionid");

            this.checkFinalize = postData =>
                postData["steamid"].Equals(this.sessionData.SteamID.ToString()) &&
                postData["access_token"].Equals(this.sessionData.OAuthToken) &&
                postData["authenticator_code"].Equals(TestSteamGuardCode) &&
                postData["activation_code"].Equals(TestSmsCode) &&
                postData["authenticator_time"].Length > 1;
        }

        [TestInitialize]
        public void Init()
        {
            this.linker = new AuthenticatorLinker(this.sessionData);
        }

        [TestMethod]
        public void TestSmsCheck()
        {
            this.linker.PhoneNumber = "test-phone";

            var mock = new SteamWebMock();
            mock.WithArgs("Request", APIEndpoints.COMMUNITY_BASE + "/steamguard/phoneajax", "POST", this.checkSmsCode)("{success: false}");

            this.linker.FinalizeAddAuthenticator(mock, TestSmsCode, response =>
            {
                Assert.AreEqual(AuthenticatorLinker.FinalizeResult.BadSMSCode, response);
            });

            Assert.AreEqual(1, mock.CallCount("Request", APIEndpoints.COMMUNITY_BASE + "/steamguard/phoneajax", "POST", this.checkSmsCode));
        }

        [TestMethod]
        public void TestGenerateCode()
        {
            var mock = new SteamWebMock();
            var guardMock = new SteamGuardAccountMock();
            guardMock.WithArgs("GenerateSteamGuardCode")(TestSteamGuardCode);

            this.linker.LinkedAccount = guardMock;

            this.linker.FinalizeAddAuthenticator(mock, TestSmsCode, response =>
            {
                Assert.AreEqual(AuthenticatorLinker.FinalizeResult.GeneralFailure, response);
            });
        }

        [TestMethod]
        public void TestErrorCodes()
        {
            var mock = new SteamWebMock();
            mock.WithArgs("MobileLoginRequest", APIEndpoints.STEAMAPI_BASE + "/ITwoFactorService/FinalizeAddAuthenticator/v0001", "POST", this.checkFinalize)("{response: {server_time: 0, status: 89, want_more: false, success: false}}");

            var guardMock = new SteamGuardAccountMock();
            guardMock.WithArgs("GenerateSteamGuardCode")(TestSteamGuardCode);

            this.linker.LinkedAccount = guardMock;

            this.linker.FinalizeAddAuthenticator(mock, TestSmsCode, response =>
            {
                Assert.AreEqual(AuthenticatorLinker.FinalizeResult.BadSMSCode, response);
            });

            mock = new SteamWebMock();
            mock.WithArgs("MobileLoginRequest", APIEndpoints.STEAMAPI_BASE + "/ITwoFactorService/FinalizeAddAuthenticator/v0001", "POST", this.checkFinalize)("{response: {server_time: 0, status: 0, want_more: true, success: false}}");

            this.linker.FinalizeAddAuthenticator(mock, TestSmsCode, response =>
            {
                Assert.AreEqual(AuthenticatorLinker.FinalizeResult.GeneralFailure, response);
            });

            mock = new SteamWebMock();
            mock.WithArgs("MobileLoginRequest", APIEndpoints.STEAMAPI_BASE + "/ITwoFactorService/FinalizeAddAuthenticator/v0001", "POST", this.checkFinalize)("{response: {server_time: 0, status: 88, want_more: true, success: false}}");

            this.linker.FinalizeAddAuthenticator(mock, TestSmsCode, response =>
            {
                Assert.AreEqual(AuthenticatorLinker.FinalizeResult.GeneralFailure, response);
            });

            mock = new SteamWebMock();
            mock.WithArgs("MobileLoginRequest", APIEndpoints.STEAMAPI_BASE + "/ITwoFactorService/FinalizeAddAuthenticator/v0001", "POST", this.checkFinalize)("{response: {server_time: 0, status: 0, want_more: true, success: true}}");

            this.linker.FinalizeAddAuthenticator(mock, TestSmsCode, response =>
            {
                Assert.AreEqual(AuthenticatorLinker.FinalizeResult.GeneralFailure, response);
            });

            mock = new SteamWebMock();
            mock.WithArgs("MobileLoginRequest", APIEndpoints.STEAMAPI_BASE + "/ITwoFactorService/FinalizeAddAuthenticator/v0001", "POST", this.checkFinalize)("{response: {server_time: 0, status: 88, want_more: true, success: true}}");

            this.linker.FinalizeAddAuthenticator(mock, TestSmsCode, response =>
            {
                Assert.AreEqual(AuthenticatorLinker.FinalizeResult.UnableToGenerateCorrectCodes, response);
            });
        }

        [TestMethod]
        public void TestLinkAuth()
        {
            var mock = new SteamWebMock();
            mock.WithArgs("MobileLoginRequest", APIEndpoints.STEAMAPI_BASE + "/ITwoFactorService/FinalizeAddAuthenticator/v0001", "POST", this.checkFinalize)("{response: {server_time: 0, status: 1, want_more: false, success: true}}");

            var guardMock = new SteamGuardAccountMock();
            guardMock.WithArgs("GenerateSteamGuardCode")(TestSteamGuardCode);

            this.linker.LinkedAccount = guardMock;

            this.linker.FinalizeAddAuthenticator(mock, TestSmsCode, response =>
            {
                Assert.AreEqual(AuthenticatorLinker.FinalizeResult.Success, response);
            });

            Assert.IsTrue(this.linker.LinkedAccount.FullyEnrolled);
        }
    }
}
