using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Geeklab.AudiencelabSDK;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Advertisements;
using UnityEngine.TestTools;


public class AudiencelabSDK_WebRequestTests {
    private static int secToStopTest = 5;
    private readonly WaitForSeconds waitForSeconds = new WaitForSeconds(0.1f);

    

    [UnityTest]
    public IEnumerator SendPurchaseMetrics() {
        var elapsedTime = 0f;
        while ((AudiencelabSDK.Instance == null || WebRequestManager.Instance == null)
               && elapsedTime < secToStopTest)
        {
            yield return waitForSeconds;
            elapsedTime += 0.1f;
        }

        var postData = new List<object>
        {
            new { id = "item1", price = 10 },
        };
        var task = AudiencelabSDK.SendCustomPurchaseMetrics(postData);
        yield return new WaitUntil(() => task.IsCompleted);

        var isSuccessPurchase = task.Result;

        
        while (isSuccessPurchase == null && elapsedTime < secToStopTest)
        {
            yield return waitForSeconds;
            elapsedTime += 0.1f;
        }

        // If we exceeded the timeout duration, fail the test.
        if (elapsedTime >= secToStopTest)
        {
            Assert.Fail("Test timed out.");
        }
        
        // Verify that the ShowAd method was called on the AdMetrics instance
        Assert.IsTrue(isSuccessPurchase);
    }
    
    
    [UnityTest]
    public IEnumerator SendAdMetrics() {
        var elapsedTime = 0f;
        while ((AudiencelabSDK.Instance == null || WebRequestManager.Instance == null)
               && elapsedTime < secToStopTest)
        {
            yield return waitForSeconds;
            elapsedTime += 0.1f;
        }

        var postData = new List<object>
        {
            new { id = "item1", price = 10 },
        };
        var task = AudiencelabSDK.SendCustomAdMetrics(postData);
        yield return new WaitUntil(() => task.IsCompleted);

        var isSuccessPurchase = task.Result;

        
        while (isSuccessPurchase == null && elapsedTime < secToStopTest)
        {
            yield return waitForSeconds;
            elapsedTime += 0.1f;
        }

        // If we exceeded the timeout duration, fail the test.
        if (elapsedTime >= secToStopTest)
        {
            Assert.Fail("Test timed out.");
        }
        
        // Verify that the ShowAd method was called on the AdMetrics instance
        Assert.IsTrue(isSuccessPurchase);
    }
    
    
    [UnityTest]
    public IEnumerator SendUserMetrics() {
        var elapsedTime = 0f;
        while ((AudiencelabSDK.Instance == null || WebRequestManager.Instance == null)
               && elapsedTime < secToStopTest)
        {
            yield return waitForSeconds;
            elapsedTime += 0.1f;
        }
        
        var postData = new List<object>
        {
            new { id = "test" },
        };
        var task = AudiencelabSDK.SendUserMetrics(postData);
        yield return new WaitUntil(() => task.IsCompleted);

        var isSuccessPurchase = task.Result;

        
        while (isSuccessPurchase == null && elapsedTime < secToStopTest)
        {
            yield return waitForSeconds;
            elapsedTime += 0.1f;
        }

        // If we exceeded the timeout duration, fail the test.
        if (elapsedTime >= secToStopTest)
        {
            Assert.Fail("Test timed out.");
        }
        
        // Verify that the ShowAd method was called on the AdMetrics instance
        Assert.IsTrue(isSuccessPurchase);
    }

}