﻿# Security Policy

## 📢 Reporting a Vulnerability

The **Cloud Streams** team and community take security vulnerabilities very seriously. **Responsible disclosure** of security issues is greatly appreciated, and we will make every effort to acknowledge and address your findings as quickly as possible.

To report a security issue:

- **Use the GitHub Security Advisory**: Please submit your report through the ["Report a Vulnerability"](https://github.com/neuroglia-io/cloud-streams/security/advisories/new) tab on GitHub.

After submitting your report, the Cloud Streams team will:

1. **Acknowledge receipt** of your report.
2. **Assess the vulnerability** and determine its severity.
3. **Provide details on next steps**, including potential mitigation strategies.
4. **Keep you updated** on the progress toward a resolution.
5. **Coordinate disclosure** and issue any necessary security patches.

If additional information is required, the team may request further details to assist in investigating and resolving the issue.

---

## 🔐 Security Best Practices

To **enhance the security and stability** of Cloud Streams, follow these **best practices**:

### **1️⃣ Secure the Deployment Environment**
- Ensure **Cloud Streams** is deployed on a **hardened and up-to-date infrastructure**.
- Apply **security patches** regularly.
- Configure **firewalls** and **network security policies** to restrict access to essential services.

### **2️⃣ Secure Configuration Management**
- Store **configuration files** securely, especially those containing **database credentials** or **API keys**.
- Use **environment variables** or **secret management tools** (e.g., HashiCorp Vault, AWS Secrets Manager) to avoid hardcoding sensitive information.

### **3️⃣ Event Security and Validation**
- Ensure that **ingested CloudEvents** are properly **validated** before processing.
- Use **schema validation** (`JsonSchema`) to enforce **event structure correctness** and prevent injection attacks.
- Implement **rate limiting** to prevent abuse from excessive event submissions.

### **4️⃣ Access Control and Authentication**
- Enforce **Role-Based Access Control (RBAC)** to define granular permissions.
- Restrict access to **projection types**, **relationships**, and **triggers** based on **user roles**.
- Integrate with **identity providers** (OAuth2, OIDC, LDAP) for authentication and authorization.

### **5️⃣ Container Security (if applicable)**
- Use **hardened base images** for containerized deployments.
- Regularly **scan container images** for vulnerabilities (e.g., using Trivy or Clair).
- Avoid running Cloud Streams containers with **elevated privileges**.

### **6️⃣ Secure Communication Channels**
- Use **TLS encryption** for all communication between Cloud Streams and **external services** (e.g., APIs, databases).
- Validate **TLS certificates** and enable **mutual TLS** where applicable.
- Prevent **man-in-the-middle attacks** by enforcing strict **certificate validation**.

### **7️⃣ Logging and Monitoring**
- Enable **audit logging** for **CloudEvent ingestion**, **projection updates**, and **query executions**.
- Implement **real-time monitoring** to detect unusual activity (e.g., unauthorized modifications to projections).
- Use tools like **Prometheus**, **Grafana**, or **ELK Stack** to monitor system health and security metrics.

### **8️⃣ Regular Security Audits**
- Conduct **regular security reviews**, including **code audits**, **penetration testing**, and **dependency scanning**.
- Address **vulnerabilities** in Cloud Streams dependencies as soon as security updates are available.

### **9️⃣ Incident Response and Recovery**
- Maintain a **well-documented incident response plan** for Cloud Streams.
- Ensure **automated backups** are **encrypted** and **regularly tested** for integrity.
- In the event of a security breach, follow the **containment, remediation, and recovery** procedures.

---

## 🛡️ Responsible Disclosure Policy

If you **discover a vulnerability** that could impact Cloud Streams **or its users**, we ask that you:

1. **Report the issue privately** via GitHub Security Advisories (linked above).
2. **Refrain from publicly disclosing the vulnerability** until a fix is available.
3. **Work with us in good faith** to ensure a timely resolution.

Once a fix is implemented, we will **publicly acknowledge** responsible disclosures in security advisories.

---

## 📩 Contact & Further Information
For general security inquiries, reach out to us through the **GitHub Discussions** or **Security Advisory** pages.

Thank you for helping keep **Cloud Streams** secure and reliable! 🚀
