package com.gearvrfix.guard;

import android.app.job.JobParameters;
import android.app.job.JobService;

public class GuardJobService extends JobService {
    @Override
    public boolean onStartJob(JobParameters params) {
        Guard.check(this, params.getJobId() == Guard.JOB_ON_CHANGE ? "setting changed" : "periodic");
        Guard.schedule(this);   // re-arm the one-shot content trigger
        return false;
    }

    @Override
    public boolean onStopJob(JobParameters params) {
        return true;
    }
}
