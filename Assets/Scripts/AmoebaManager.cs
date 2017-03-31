﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class AmoebaManager : MonoBehaviour
{
	public StressorController stressorTemplate;

	public float stressLevel { get; private set; }

	public float redStressCapacity = 50f;
	//	public float redDecayUnitsPerSecond = 2f;
	public const float redStressDecayRatio = 0.05f;

	// units per second
	public float maxColorAdaptionSpeed = 0.1f;

	public const float capacitySizeFactor = 50;

	private Renderer rend;

	private Color currentColor = Color.white;
	private Color inverseColor = Color.black;

	public BreathingController breathingController;

	private const float lightPeakIntensity = 3;
	private const float lightIntensityIncreaseDuration = 0.2f;
	private const float lightIntensityDecreaseDuration = 0.5f;

	private bool lightIntensityIsIncreasing = false;
	private float lightIntensityTarget;

	public const float growthFactor = 0.2f;

	private float maxStressSinceGrowth = 0;

	private List<GameObject> listeners = new List<GameObject> ();

	private bool isLashingOut = false;
	private float lastOutburst = 0;
	public const float baseOutburstFrequency = 3;
	public const int numberStressorsProducedDuringOutburst = 5;

	void Start ()
	{
		rend = GetComponent<Renderer> ();
		rend.material.color = currentColor;
		stressLevel = 0;
	}

	void OnTriggerEnter (Collider other)
	{
		if (other.gameObject.CompareTag ("Stressor")) {
			absorbStress (other.gameObject);
		}
	}

	void absorbStress (GameObject gameObject)
	{
		StressorController stressor = (StressorController)gameObject.GetComponent<StressorController> ();
		if (stressor.creator == this.gameObject) {
			return;
		}

		gameObject.SetActive (false);
		stressLevel += stressor.stressLevel;
		Destroy (gameObject);
	}

	void Update ()
	{
		UpdateLashout ();
		UpdateStress ();
		UpdateBreathingSpeed ();
		UpdateColors ();
		UpdateSize ();
		UpdateLight ();
	}

	void UpdateColors ()
	{
		float redDelta = (stressLevel / redStressCapacity) - inverseColor.r;
		if (redDelta != 0) {
			float newRed = inverseColor.r + Mathf.Min (redDelta * Time.deltaTime, maxColorAdaptionSpeed);
			inverseColor = new Color (newRed, inverseColor.g, inverseColor.b);
		}

		currentColor = new Color (1 - inverseColor.b - inverseColor.g,
			1 - inverseColor.b - inverseColor.r, 1 - inverseColor.r - inverseColor.g);

		rend.material.color = currentColor;
	}

	void UpdateStress ()
	{
		if (stressLevel >= redStressCapacity) {
			maxCapacityReached ();
		}

		if (isLashingOut) {
			// special logic will be done when lashing out
			return;
		}

		if (stressLevel > maxStressSinceGrowth) {
			maxStressSinceGrowth = stressLevel;
		}

		if (stressLevel < redStressCapacity && stressLevel > 0) {
			stressLevel -= redStressCapacity * redStressDecayRatio * Time.deltaTime;
		}

		if (stressLevel <= 0 && maxStressSinceGrowth > 0) {
			triggerGrowth ();
			stressLevel = 0;
			maxStressSinceGrowth = 0;
		}
	}

	void UpdateBreathingSpeed ()
	{
		breathingController.setSpeedFactor (stressLevel / redStressCapacity);
	}

	void UpdateSize ()
	{
		float sizeRatio = redStressCapacity / capacitySizeFactor;
		sizeRatio = Mathf.Log (sizeRatio + 1) + 0.5f;
		breathingController.setBaseFactor (sizeRatio);
	}

	void UpdateLight ()
	{
		// TODO might need to put logic to pulse correctly in the future
		Light light = GetComponent<Light> ();

		if (lightIntensityIsIncreasing) {
			light.intensity += Time.deltaTime * lightIntensityTarget / lightIntensityIncreaseDuration;
			lightIntensityIsIncreasing = light.intensity < lightIntensityTarget;
		} else if (light.intensity > 0) {
			light.intensity -= Time.deltaTime * lightIntensityTarget;
		}
	}

	void UpdateLashout ()
	{
		if (!isLashingOut) {
			// Don't need to do work if we aren't lashing out.
			return;
		}

		float timeSinceLastOutburst = Time.time - lastOutburst;
		float timeBetweenOutbursts = 1 / baseOutburstFrequency;

		if (timeSinceLastOutburst > timeBetweenOutbursts) {
			triggerOutburst ();
			lastOutburst = Time.time;
		}

		float fluctuationScale = timeSinceLastOutburst / timeBetweenOutbursts * -2 * breathingController.fluctuation + 1;
		this.transform.localScale = Vector3.one * breathingController.baseFactor * fluctuationScale;
	}

	void triggerGrowth ()
	{
		triggerIntensityIncrease (maxStressSinceGrowth / redStressCapacity);
		redStressCapacity += maxStressSinceGrowth * growthFactor;
	}

	void triggerIntensityIncrease (float percentage)
	{
		lightIntensityIsIncreasing = true;
		// TODO possible log here?
		lightIntensityTarget = percentage * lightPeakIntensity;
	}

	private void maxCapacityReached ()
	{
		if (!isLashingOut) {
			notifyListeners ();
		}
		stressLevel = redStressCapacity;
		beginLashout ();
	}

	private void beginLashout ()
	{
		isLashingOut = true;
		breathingController.pauseBreathing ();
	}

	private void endLashout ()
	{
		isLashingOut = false;
		breathingController.resumeBreathing ();
	}

	private void triggerOutburst ()
	{
		float increament = 360f / numberStressorsProducedDuringOutburst;
		float outburstStressLevel = (float)redStressCapacity / 8;

		for (int i = 0; i < numberStressorsProducedDuringOutburst; i++) {
			float angle = increament * i;
			Quaternion quaternion = Quaternion.AngleAxis (angle, Vector3.up);

			Vector3 forceUnitVector = quaternion * new Vector3 (1, 0, 1);

			StressorController stressor = (StressorController)
				Instantiate (stressorTemplate, this.transform.position, Quaternion.identity);
			stressor.applyForce (forceUnitVector * 220); // TODO replace hard coded force scalor
			stressor.creator = this.gameObject;
			stressor.setStressLevel (outburstStressLevel);

			// Kill in 5 seconds
			Destroy (stressor.gameObject, 1);
		}

		// TODO this should probably just be part of the breathingController
		this.transform.localScale = Vector3.one * breathingController.baseFactor * (1 + breathingController.fluctuation);
		stressLevel -= outburstStressLevel;

		if (stressLevel < redStressCapacity / 2) {
			endLashout ();
		}
	}

	private void notifyListeners ()
	{
		foreach (GameObject target in listeners) {
			ExecuteEvents.Execute<IMaxStressTarget> (target, null, (x, y) => x.MaxStressReached (this));
		}
	}

	public void registerListener (GameObject target)
	{
		listeners.Add (target);
	}
}