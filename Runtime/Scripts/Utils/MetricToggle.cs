using UnityEngine;
using System.Collections;


namespace Geeklab.AudiencelabSDK
{
    public class MetricToggle : MonoBehaviour
    {
        private bool dataCollectionActive;
        private readonly WaitForSeconds waitForSeconds = new WaitForSeconds(5f);

        public void InitializeMetrics()
        {
            StartCoroutine(CheckDataCollectionStatus());
        }

        private IEnumerator CheckDataCollectionStatus()
        {
            while (true)
            {
                WebRequestManager.Instance.CheckDataCollectionStatusRequest(
                    (response) =>
                    {
                        if (SDKSettingsModel.Instance.ShowDebugLog)
                            Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Success: {response}");
                        dataCollectionActive = bool.Parse(response);
                    },
                    (error) => { Debug.LogError($"{SDKSettingsModel.GetColorPrefixLog()} Error: {error}"); }
                );

                yield return waitForSeconds;
            }
        }

        public bool IsDataCollectionActive()
        {
            return dataCollectionActive;
        }
    }
}